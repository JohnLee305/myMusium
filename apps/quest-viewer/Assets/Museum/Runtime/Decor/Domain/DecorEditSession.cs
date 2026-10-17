using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FamilyMuseum.Decor
{
    public sealed class DecorEditSession
    {
        sealed class State
        {
            internal DecorWorld world;
            internal Dictionary<string, long> stored;
        }
        sealed class Receipt
        {
            internal string key;
            internal bool accepted;
            internal ValidationIssue[] issues;
        }
        readonly DecorCatalog catalog;
        readonly DecorWorld original;
        readonly Func<DecorCatalog, DecorWorld, ValidationReport> additionalValidator;
        readonly int historyLimit;
        readonly List<State> undo = new List<State>(), redo = new List<State>();
        readonly Dictionary<string, Receipt> receipts = new Dictionary<string, Receipt>(StringComparer.Ordinal);
        readonly HashSet<string> issuedInstances = new HashSet<string>(StringComparer.Ordinal);
        DecorWorld draft;
        Dictionary<string, long> stored = new Dictionary<string, long>(StringComparer.Ordinal);
        long sequence;
        bool busy, closed;
        public int UndoCount { get { return undo.Count; } }
        public int RedoCount { get { return redo.Count; } }
        public bool IsClosed { get { return closed; } }
        public DecorEditSession(DecorCatalog catalog, DecorWorld world) : this(catalog, world, null, 200) { }
        public DecorEditSession(DecorCatalog catalog, DecorWorld world, Func<DecorCatalog, DecorWorld, ValidationReport> additionalValidator, int historyLimit)
        {
            if (historyLimit < 1 || historyLimit > 10000) throw new ArgumentOutOfRangeException("historyLimit");
            if (!DecorReferenceValidator.Validate(catalog, world).ok) throw new InvalidDataException("EDIT_BASE_INVALID");
            this.catalog = DecorCopies.Catalog(catalog); original = DecorCopies.World(world); draft = DecorCopies.World(world);
            this.additionalValidator = additionalValidator; this.historyLimit = historyLimit;
            foreach (var item in world.instances) issuedInstances.Add(item.id);
            if (!Validate(draft).ok) throw new InvalidDataException("EDIT_BASE_INVALID");
        }
        public static DecorEditSession BeginEdit(DecorWorld world, DecorCatalog catalog)
        { return new DecorEditSession(catalog, world); }
        public DecorWorld BuildSnapshot() { return DecorCopies.World(draft); }
        public DecorInventorySummary Inventory() { return DecorInventoryQuery.Read(catalog, draft, stored); }
        public DecorCommandResult ApplyCommand(DecorCommand command)
        {
            if (closed) return Fail("SESSION_CLOSED");
            if (busy) return Fail("SESSION_BUSY");
            if (command == null || !Id(command.commandId)) return Fail("COMMAND_INVALID");
            command = new DecorCommand { commandId = command.commandId, kind = command.kind, instanceId = command.instanceId,
                variantId = command.variantId, definition = command.definition == null ? null : new DefinitionRef { id = command.definition.id, version = command.definition.version },
                location = DecorCopies.Location(command.location) };
            busy = true;
            try
            {
                string key = DecorKeys.Command(command); Receipt receipt;
                if (receipts.TryGetValue(command.commandId, out receipt))
                {
                    if (receipt.key != key) return Fail("COMMAND_ID_REUSED", command.instanceId);
                    return Result(receipt.accepted, false, true, false, receipt.issues);
                }
                var shapeError = CommandShape(command);
                if (shapeError != null) return Remember(command.commandId, key, false, new[] { shapeError });
                var candidate = DecorCopies.World(draft);
                var item = candidate.instances.FirstOrDefault(i => i.id == command.instanceId);
                if (command.kind == "CreateAndPlace")
                {
                    if (issuedInstances.Contains(command.instanceId)) return Remember(command.commandId, key, false, Issues("INSTANCE_ALREADY_EXISTS", command.instanceId));
                    item = new ItemInstance { id = command.instanceId, definition = new DefinitionRef { id = command.definition.id, version = command.definition.version },
                        variantId = command.variantId, location = DecorCopies.Location(command.location) };
                    candidate.instances = candidate.instances.Concat(new[] { item }).ToArray();
                }
                else
                {
                    if (item == null) return Remember(command.commandId, key, false, Issues("INSTANCE_NOT_FOUND", command.instanceId));
                    switch (command.kind)
                    {
                        case "SetLocation":
                            if (item.location.kind == "storage") return Remember(command.commandId, key, false, Issues("ITEM_NOT_PLACED", item.id));
                            item.location = DecorCopies.Location(command.location); break;
                        case "SetVariant": item.variantId = command.variantId; break;
                        case "Store": item.location = new ItemLocation { kind = "storage" }; break;
                        case "PlaceStored":
                            if (item.location.kind != "storage") return Remember(command.commandId, key, false, Issues("ITEM_NOT_STORED", item.id));
                            item.location = DecorCopies.Location(command.location); break;
                    }
                }
                var report = Validate(candidate);
                if (!report.ok) return Remember(command.commandId, key, false, report.issues);
                if (DecorJson.WriteWorld(candidate) == DecorJson.WriteWorld(draft)) return Remember(command.commandId, key, true, new ValidationIssue[0]);

                var previous = Capture();
                undo.Add(previous); bool trimmed = undo.Count > historyLimit;
                if (trimmed) undo.RemoveAt(0);
                redo.Clear(); draft = candidate;
                if (command.kind == "CreateAndPlace") issuedInstances.Add(command.instanceId);
                if (command.kind == "Store") stored[command.instanceId] = ++sequence;
                else if (command.kind == "PlaceStored") stored.Remove(command.instanceId);
                receipts.Add(command.commandId, new Receipt { key = key, accepted = true, issues = new ValidationIssue[0] });
                return Result(true, true, false, trimmed, new ValidationIssue[0]);
            }
            finally { busy = false; }
        }
        public DecorCommandResult Undo()
        {
            if (closed) return Fail("SESSION_CLOSED"); if (busy) return Fail("SESSION_BUSY");
            if (undo.Count == 0) return Fail("NOTHING_TO_UNDO");
            redo.Add(Capture()); Restore(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1);
            return Result(true, true, false, false, new ValidationIssue[0]);
        }
        public DecorCommandResult Redo()
        {
            if (closed) return Fail("SESSION_CLOSED"); if (busy) return Fail("SESSION_BUSY");
            if (redo.Count == 0) return Fail("NOTHING_TO_REDO");
            undo.Add(Capture()); Restore(redo[redo.Count - 1]); redo.RemoveAt(redo.Count - 1);
            return Result(true, true, false, false, new ValidationIssue[0]);
        }
        public DecorCommandResult Cancel()
        {
            if (closed) return Fail("SESSION_CLOSED"); if (busy) return Fail("SESSION_BUSY");
            bool changed = DecorJson.WriteWorld(draft) != DecorJson.WriteWorld(original);
            draft = DecorCopies.World(original); stored.Clear(); undo.Clear(); redo.Clear(); receipts.Clear(); closed = true;
            return Result(true, changed, false, false, new ValidationIssue[0]);
        }
        State Capture() { return new State { world = DecorCopies.World(draft), stored = new Dictionary<string, long>(stored, StringComparer.Ordinal) }; }
        void Restore(State state) { draft = DecorCopies.World(state.world); stored = new Dictionary<string, long>(state.stored, StringComparer.Ordinal); }
        ValidationReport Validate(DecorWorld candidate)
        {
            var baseline = DecorReferenceValidator.Validate(catalog, candidate);
            if (!baseline.ok || additionalValidator == null) return baseline;
            try
            {
                // A future geometry validator cannot mutate either the session or the candidate.
                var result = additionalValidator(DecorCopies.Catalog(catalog), DecorCopies.World(candidate));
                if (result == null) return new ValidationReport(Issues("VALIDATOR_FAILED", "session"));
                return new ValidationReport(DecorCopies.Issues(result.issues));
            }
            catch (Exception) { return new ValidationReport(Issues("VALIDATOR_FAILED", "session")); }
        }
        DecorCommandResult Remember(string id, string key, bool accepted, ValidationIssue[] issues)
        {
            receipts.Add(id, new Receipt { key = key, accepted = accepted, issues = DecorCopies.Issues(issues) });
            return Result(accepted, false, false, false, issues);
        }
        DecorCommandResult Result(bool accepted, bool changed, bool duplicate, bool trimmed, ValidationIssue[] issues)
        { return new DecorCommandResult(accepted, changed, duplicate, trimmed, draft, issues); }
        DecorCommandResult Fail(string code, string id = "session") { return Result(false, false, false, false, Issues(code, id)); }
        static ValidationIssue[] Issues(string code, string id) { return new[] { new ValidationIssue(code, id) }; }
        static bool Id(string value) { return value != null && Regex.IsMatch(value, @"\A[a-z][a-z0-9.-]{0,63}\z"); }
        static ValidationIssue CommandShape(DecorCommand c)
        {
            bool valid = Id(c.instanceId);
            switch (c.kind)
            {
                case "CreateAndPlace": valid &= c.definition != null && c.variantId != null && c.location != null && c.location.kind != "storage"; break;
                case "SetLocation": case "PlaceStored": valid &= c.definition == null && c.variantId == null && c.location != null && c.location.kind != "storage"; break;
                case "SetVariant": valid &= c.definition == null && c.location == null && c.variantId != null; break;
                case "Store": valid &= c.definition == null && c.variantId == null && c.location == null; break;
                default: valid = false; break;
            }
            return valid ? null : new ValidationIssue("COMMAND_INVALID", c.instanceId);
        }
    }
}
