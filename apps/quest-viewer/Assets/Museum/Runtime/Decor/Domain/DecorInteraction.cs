using System;
using System.Linq;

namespace FamilyMuseum.Decor
{
    // Input-independent transaction coordinator. A preview is never a command/history entry.
    public sealed class DecorInteraction
    {
        readonly DecorCatalog catalog;
        readonly Func<string, string> ids;
        readonly DecorEditSession session;
        ItemInstance preview;
        bool creating, stored;
        double pendingYaw;
        public string SelectedId { get; private set; }
        public bool HasPreview { get { return preview != null; } }
        public bool IsCreatingPreview { get { return HasPreview && creating; } }
        public bool PreviewValid { get; private set; }
        public ValidationIssue[] PreviewIssues { get; private set; } = new ValidationIssue[0];
        public int UndoCount { get { return session.UndoCount; } }
        public int RedoCount { get { return session.RedoCount; } }
        public DecorWorld Snapshot() { return session.BuildSnapshot(); }
        public DecorInventorySummary Inventory() { return session.Inventory(); }
        public ItemInstance PreviewItem { get { return preview == null ? null : Copy(preview); } }
        public DecorInteraction(DecorCatalog catalog, DecorWorld world, Func<string, string> ids = null)
        {
            this.catalog = DecorCopies.Catalog(catalog); session = DecorPlacementValidator.BeginEdit(catalog, world);
            this.ids = ids ?? (prefix => prefix + "." + Guid.NewGuid().ToString("N"));
        }
        public ItemDefinition Definition(ItemInstance item)
        { return catalog.items.First(d => d.id == item.definition.id && d.version == item.definition.version); }
        static ItemInstance Copy(ItemInstance item)
        { return new ItemInstance { id = item.id, definition = new DefinitionRef { id = item.definition.id, version = item.definition.version }, variantId = item.variantId, location = DecorCopies.Location(item.location) }; }
        public void Select(string id) { CancelPreview(); SelectedId = Snapshot().instances.Any(i => i.id == id) ? id : null; }
        public void BeginNew(ItemDefinition definition, string variantId = null)
        {
            CancelPreview(); SelectedId = null;
            var canonical = catalog.items.First(d => d.id == definition.id && d.version == definition.version);
            preview = new ItemInstance { id = ids("item"), definition = new DefinitionRef { id = canonical.id, version = canonical.version }, variantId = string.IsNullOrEmpty(variantId) ? canonical.variants[0].id : variantId, location = new ItemLocation { kind = "storage" } };
            creating = true; stored = false; Invalidate("TARGET_REQUIRED");
            pendingYaw = 0;
        }
        public bool BeginMove(string id)
        {
            var item = Snapshot().instances.FirstOrDefault(i => i.id == id); if (item == null) return false;
            CancelPreview(); SelectedId = id; preview = Copy(item); creating = false; stored = item.location.kind == "storage";
            pendingYaw = item.location.yawDeg ?? 0;
            if (stored) Invalidate("TARGET_REQUIRED"); else Recheck(); return true;
        }
        public void SetTarget(ItemLocation location)
        {
            if (!HasPreview) return;
            preview.location = DecorCopies.Location(location); pendingYaw = location.yawDeg ?? pendingYaw; Recheck();
        }
        public void Invalidate(string code)
        { PreviewValid = false; PreviewIssues = new[] { new ValidationIssue(code, preview?.id) }; }
        public void RotatePreview(int direction)
        {
            if (!HasPreview) return;
            pendingYaw = DecorGeometry.NormalizeYaw(pendingYaw + direction * 90);
            if (preview.location.kind != "storage")
            { preview.location.yawDeg = pendingYaw; Recheck(); }
        }
        public double PreviewYaw { get { return HasPreview ? pendingYaw : 0; } }
        public DecorWorld PreviewWorld()
        {
            var world = Snapshot(); if (!HasPreview) return world;
            if (creating) world.instances = world.instances.Concat(new[] { Copy(preview) }).ToArray();
            else world.instances.First(i => i.id == preview.id).location = DecorCopies.Location(preview.location);
            return world;
        }
        void Recheck()
        {
            if (preview.location.kind == "storage") { Invalidate("TARGET_REQUIRED"); return; }
            var report = DecorPlacementValidator.Validate(catalog, PreviewWorld()); PreviewValid = report.ok; PreviewIssues = report.issues;
        }
        public DecorCommandResult Commit(bool repeat)
        {
            if (!HasPreview || !PreviewValid) return null;
            var current = Copy(preview); bool wasCreating = creating;
            var command = creating ? DecorCommands.CreateAndPlace(ids("cmd"), current.id, current.definition, current.variantId, current.location)
                : stored ? DecorCommands.PlaceStored(ids("cmd"), current.id, current.location) : DecorCommands.SetLocation(ids("cmd"), current.id, current.location);
            var result = session.ApplyCommand(command);
            if (result.accepted)
            {
                CancelPreview(); SelectedId = current.id;
                if (repeat && wasCreating) { BeginNew(Definition(current), current.variantId); SetTarget(current.location); }
            }
            return result;
        }
        public void CancelPreview() { preview = null; creating = stored = false; PreviewValid = false; PreviewIssues = new ValidationIssue[0]; }
        public DecorCommandResult StoreSelected()
        { CancelPreview(); return SelectedId == null ? null : session.ApplyCommand(DecorCommands.Store(ids("cmd"), SelectedId)); }
        public DecorCommandResult SetVariant(string variant)
        {
            if (HasPreview) { if (!creating) return null; preview.variantId = variant; if (preview.location.kind != "storage") Recheck(); return null; }
            return SelectedId == null ? null : session.ApplyCommand(DecorCommands.SetVariant(ids("cmd"), SelectedId, variant));
        }
        public DecorCommandResult RotateSelected(int direction)
        {
            if (HasPreview) { RotatePreview(direction); return null; }
            var item = Snapshot().instances.FirstOrDefault(i => i.id == SelectedId);
            if (item == null || item.location.kind == "storage") return null;
            var target = DecorCopies.Location(item.location); target.yawDeg = DecorGeometry.NormalizeYaw(target.yawDeg.Value + direction * 90);
            return session.ApplyCommand(DecorCommands.SetLocation(ids("cmd"), item.id, target));
        }
        public DecorCommandResult Undo() { CancelPreview(); var r = session.Undo(); RepairSelection(); return r; }
        public DecorCommandResult Redo() { CancelPreview(); var r = session.Redo(); RepairSelection(); return r; }
        void RepairSelection() { if (!Snapshot().instances.Any(i => i.id == SelectedId)) SelectedId = null; }
    }
}
