using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FamilyMuseum.Decor
{
    // commandId identifies a submission, instanceId identifies the owned item.
    public sealed class DecorCommand
    {
        public string commandId, kind, instanceId, variantId;
        public DefinitionRef definition;
        public ItemLocation location;
    }
    public sealed class DecorCommandResult
    {
        public readonly bool accepted, changed, duplicate, historyTrimmed;
        public readonly DecorWorld resultingDraft;
        public readonly ValidationIssue[] issues;
        internal DecorCommandResult(bool accepted, bool changed, bool duplicate, bool trimmed, DecorWorld draft, ValidationIssue[] issues)
        {
            this.accepted = accepted; this.changed = changed; this.duplicate = duplicate; historyTrimmed = trimmed;
            resultingDraft = DecorCopies.World(draft); this.issues = DecorCopies.Issues(issues);
        }
    }
    public static class DecorCommands
    {
        public static DecorCommand CreateAndPlace(string commandId, string instanceId, DefinitionRef definition, string variantId, ItemLocation location)
        { return new DecorCommand { kind = "CreateAndPlace", commandId = commandId, instanceId = instanceId, definition = definition, variantId = variantId, location = location }; }
        public static DecorCommand SetLocation(string commandId, string instanceId, ItemLocation location)
        { return new DecorCommand { kind = "SetLocation", commandId = commandId, instanceId = instanceId, location = location }; }
        public static DecorCommand SetVariant(string commandId, string instanceId, string variantId)
        { return new DecorCommand { kind = "SetVariant", commandId = commandId, instanceId = instanceId, variantId = variantId }; }
        public static DecorCommand Store(string commandId, string instanceId)
        { return new DecorCommand { kind = "Store", commandId = commandId, instanceId = instanceId }; }
        public static DecorCommand PlaceStored(string commandId, string instanceId, ItemLocation location)
        { return new DecorCommand { kind = "PlaceStored", commandId = commandId, instanceId = instanceId, location = location }; }
    }
    internal static class DecorCopies
    {
        internal static DecorWorld World(DecorWorld source)
        {
            DecorWorld result; ValidationReport report;
            if (!DecorJson.TryReadWorld(DecorJson.WriteWorld(source), out result, out report)) throw new InvalidOperationException("WORLD_COPY_FAILED");
            return result;
        }
        internal static DecorCatalog Catalog(DecorCatalog source)
        {
            DecorCatalog result; ValidationReport report;
            if (!DecorJson.TryReadCatalog(DecorJson.WriteCatalog(source), out result, out report)) throw new InvalidOperationException("CATALOG_COPY_FAILED");
            return result;
        }
        internal static ItemLocation Location(ItemLocation source)
        {
            if (source == null) return null;
            return new ItemLocation { kind = source.kind, roomId = source.roomId, positionM = Vector(source.positionM), parentId = source.parentId, surfaceId = source.surfaceId, localPositionM = Vector(source.localPositionM), yawDeg = source.yawDeg };
        }
        static double[] Vector(double[] source) { return source == null ? null : (double[])source.Clone(); }
        internal static ValidationIssue[] Issues(ValidationIssue[] source)
        {
            return source.Select(i => new ValidationIssue(i.code, i.primaryId, (string[])i.relatedIds.Clone()) { roomId = i.roomId, position = Vector(i.position) }).ToArray();
        }
    }
    // Length-prefixed tokens avoid collisions between user strings. Keys are session metadata, not save format.
    internal static class DecorKeys
    {
        internal static string Token(string value) { return value == null ? "-:" : value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value; }
        internal static string Number(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        internal static string Vector(double[] value) { return value == null ? "-:" : Token(string.Join(",", value.Select(Number))); }
        internal static string Location(ItemLocation l)
        {
            if (l == null) return "-:";
            return Token(l.kind) + Token(l.roomId) + Vector(l.positionM) + Token(l.parentId) + Token(l.surfaceId) + Vector(l.localPositionM) + Token(l.yawDeg.HasValue ? Number(l.yawDeg.Value) : null);
        }
        internal static string Definition(DefinitionRef d) { return d == null ? "-:" : Token(d.id) + Token(d.version.ToString(CultureInfo.InvariantCulture)); }
        internal static string Command(DecorCommand c)
        { return Token(c.kind) + Token(c.instanceId) + Definition(c.definition) + Token(c.variantId) + Location(c.location); }
    }
}
