using System;
using System.Linq;

namespace FamilyMuseum.Decor
{
    // Owned by the editor workspace, never by a PC/VR view. Dispose only on workspace shutdown.
    public sealed class DecorEditorSession : IDisposable
    {
        readonly DecorCatalog catalog;
        readonly DecorPrototypePersistence persistence;
        readonly IDecorWorldPointer pointer;
        readonly Func<DecorWorld, IDecorPreparedView> prepare;
        DecorInteraction interaction;
        string candidateToken, confirmationToken;
        bool disposed;
        public DecorInputCoordinator Input { get; private set; }
        public bool Editing { get; private set; }
        public bool Snap { get; private set; } = true;
        public bool Repeat { get; private set; }
        public bool Busy { get; private set; }
        public bool CanEdit { get { return !disposed && !Busy && Editing && confirmationToken == null && persistence.CanEdit && interaction != null; } }
        public string State { get { return disposed ? "closed" : persistence.State; } }
        public string SelectedId { get { return interaction?.SelectedId; } }
        public int UndoCount { get { return interaction?.UndoCount ?? 0; } }
        public int RedoCount { get { return interaction?.RedoCount ?? 0; } }
        public bool HasPreview { get { return interaction != null && interaction.HasPreview; } }
        public bool PreviewValid { get { return HasPreview && interaction.PreviewValid; } }
        public string ConfirmationToken { get { return confirmationToken; } }
        public DecorWorld Snapshot() { return interaction?.Snapshot(); }
        public DecorWorld PreviewWorld() { return interaction?.PreviewWorld(); }
        public ItemInstance PreviewItem { get { return interaction?.PreviewItem; } }
        public DecorEditorSession(string storeRoot, DecorCatalog catalog, DecorWorld fixture,
            IDecorWorldPointer pointer, Func<DecorWorld, IDecorPreparedView> prepare, DecorInputSource source = DecorInputSource.PC)
        {
            this.catalog = DecorCopies.Catalog(catalog); this.pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            this.prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            persistence = new DecorPrototypePersistence(storeRoot, catalog, fixture);
            if (persistence.Baseline != null) interaction = new DecorInteraction(catalog, persistence.Baseline);
            Editing = persistence.CanEdit; Input = new DecorInputCoordinator(source);
        }
        public bool SwitchMode(DecorInputSource source)
        {
            if (disposed || Busy || !Enum.IsDefined(typeof(DecorInputSource), source)) return false;
            CancelPreview(); Input.Switch(source); return true;
        }
        public bool Suspend()
        { if (disposed || Busy) return false; CancelPreview(); Input.Suspend(); return true; }
        public bool Resume()
        { if (disposed || Busy) return false; Input.Resume(); return true; }
        void CancelPreview() { interaction?.CancelPreview(); candidateToken = null; Input.ClearPreview(); }
        DecorIntentResult Result(string status, string reason = null, bool accepted = false)
        { return new DecorIntentResult { status = status, reasonCode = reason, commandAccepted = accepted, candidateToken = candidateToken, confirmationToken = confirmationToken }; }
        DecorIntentResult Command(DecorCommandResult command)
        {
            candidateToken = null;
            if (!HasPreview) Input.ClearPreview();
            return command == null ? Result("NoChange") : command.accepted ? Result("Accepted", null, true) :
                Result("Rejected", command.issues.FirstOrDefault()?.code ?? "COMMAND_REJECTED");
        }
        public DecorIntentResult Submit(DecorEditIntent intent, bool uiConsumed = false, bool textEditing = false)
        {
            if (disposed) return Result("Rejected", "SESSION_CLOSED");
            if (Busy) return Result("Rejected", "SESSION_BUSY");
            var denied = Input.Admit(intent, uiConsumed, textEditing);
            if (denied != null) return Result("Rejected", denied);
            if (intent.action == DecorEditAction.ReleaseControls) return Result("NoChange");
            if (intent.action == DecorEditAction.Reload) return Reload();
            if (intent.action == DecorEditAction.ConfirmDiscard)
            {
                if (confirmationToken == null || intent.confirmationToken != confirmationToken) return Result("Rejected", "STALE_CONFIRMATION");
                if (!persistence.CanEdit) return Result("Rejected", "SESSION_NOT_EDITABLE");
                interaction = new DecorInteraction(catalog, persistence.Cancel()); Editing = false; confirmationToken = null; CancelPreview(); return Result("Accepted");
            }
            if (intent.action == DecorEditAction.ResumeEdit)
            {
                if (confirmationToken != null) { confirmationToken = null; return Result("Accepted"); }
                if (!persistence.CanEdit) return Result("Rejected", "SESSION_NOT_EDITABLE");
                if (Editing) return Result("NoChange");
                interaction = new DecorInteraction(catalog, persistence.Baseline); Editing = true; return Result("Accepted");
            }
            if (!CanEdit) return Result("Rejected", "SESSION_NOT_EDITABLE");
            switch (intent.action)
            {
                case DecorEditAction.Select:
                case DecorEditAction.SampleDefinition:
                    var selected = Query(intent.ray, true);
                    if (selected.blocked || selected.instanceId == null) return Result("Rejected", selected.reasonCode ?? "TARGET_REQUIRED");
                    var item = interaction.Snapshot().instances.FirstOrDefault(i => i.id == selected.instanceId);
                    if (item == null) return Result("Rejected", "ITEM_NOT_FOUND");
                    CancelPreview();
                    if (intent.action == DecorEditAction.Select) interaction.Select(item.id);
                    else { interaction.BeginNew(interaction.Definition(item), item.variantId); Input.OwnPreview(intent.pointerId); }
                    return Result("Accepted");
                case DecorEditAction.BeginPlace:
                    var definition = catalog.items.FirstOrDefault(d => d.id == intent.definitionId && d.version == intent.definitionVersion);
                    if (definition == null || (intent.variantId != null && !definition.variants.Any(v => v.id == intent.variantId))) return Result("Rejected", "DEFINITION_INVALID");
                    CancelPreview(); interaction.BeginNew(definition, intent.variantId); Input.OwnPreview(intent.pointerId); return Result("Accepted");
                case DecorEditAction.BeginMove:
                    if (!interaction.Snapshot().instances.Any(i => i.id == (intent.instanceId ?? SelectedId))) return Result("Rejected", "ITEM_NOT_FOUND");
                    CancelPreview(); interaction.BeginMove(intent.instanceId ?? SelectedId);
                    // Existing location is not a fresh pointer candidate; require a visible hit before confirmation.
                    interaction.Invalidate("TARGET_REQUIRED"); Input.OwnPreview(intent.pointerId); return Result("Accepted");
                case DecorEditAction.UpdateTarget:
                    if (!HasPreview) return Result("Rejected", "PREVIEW_REQUIRED");
                    candidateToken = null; var hit = Query(intent.ray);
                    if (hit.blocked || hit.target == null) { interaction.Invalidate(hit.reasonCode ?? "TARGET_REQUIRED"); return Result("Rejected", hit.reasonCode ?? "TARGET_REQUIRED"); }
                    var target = DecorCopies.Location(hit.target); target.yawDeg = interaction.PreviewYaw;
                    interaction.SetTarget(target);
                    if (!PreviewValid) return Result("Rejected", interaction.PreviewIssues.FirstOrDefault()?.code);
                    candidateToken = Guid.NewGuid().ToString("N"); return Result("Accepted");
                case DecorEditAction.ConfirmPlacement:
                    if (!HasPreview || candidateToken == null || intent.candidateToken != candidateToken) return Result("Rejected", "STALE_CANDIDATE");
                    // Re-query on the confirmation tick; a wall/UI/tracking change must invalidate an earlier valid preview.
                    var confirmHit = Query(intent.ray); var current = interaction.PreviewItem.location;
                    if (confirmHit.blocked || confirmHit.target == null) { candidateToken = null; interaction.Invalidate(confirmHit.reasonCode ?? "TARGET_REQUIRED"); return Result("Rejected", confirmHit.reasonCode ?? "TARGET_REQUIRED"); }
                    var confirmed = DecorCopies.Location(confirmHit.target); confirmed.yawDeg = interaction.PreviewYaw;
                    interaction.SetTarget(confirmed);
                    if (!SameTarget(current, confirmed)) { candidateToken = null; return Result("Rejected", "TARGET_CHANGED"); }
                    var committed = interaction.Commit(Repeat);
                    if (HasPreview) Input.OwnPreview(intent.pointerId);
                    return Command(committed);
                case DecorEditAction.CancelPreview: CancelPreview(); return Result("Accepted");
                case DecorEditAction.ClearSelection: CancelPreview(); interaction.Select(null); return Result("Accepted");
                case DecorEditAction.RotateStep:
                    if (intent.direction != -1 && intent.direction != 1) return Result("Rejected", "DIRECTION_INVALID");
                    if (HasPreview) { interaction.RotatePreview(intent.direction); candidateToken = null; return Result("Accepted"); }
                    return Command(interaction.RotateSelected(intent.direction));
                case DecorEditAction.Store: return Command(interaction.StoreSelected());
                case DecorEditAction.SetVariant:
                    var variantItem = interaction.PreviewItem ?? interaction.Snapshot().instances.FirstOrDefault(i => i.id == SelectedId);
                    if (variantItem == null || !interaction.Definition(variantItem).variants.Any(v => v.id == intent.variantId)) return Result("Rejected", "VARIANT_INVALID");
                    if (HasPreview && interaction.IsCreatingPreview) { interaction.SetVariant(intent.variantId); candidateToken = null; return Result("Accepted"); }
                    return Command(interaction.SetVariant(intent.variantId));
                case DecorEditAction.Undo: return Command(interaction.Undo());
                case DecorEditAction.Redo: return Command(interaction.Redo());
                case DecorEditAction.SetSnap: Snap = intent.enabled; candidateToken = null; interaction.Invalidate("TARGET_REQUIRED"); return Result("Accepted");
                case DecorEditAction.SetRepeat: Repeat = intent.enabled; return Result("Accepted");
                case DecorEditAction.Apply:
                    if (HasPreview) return Result("Rejected", "PREVIEW_ACTIVE");
                    Busy = true;
                    try
                    {
                        var applied = persistence.Apply(interaction.Snapshot(), prepare);
                        if (applied.status == "activated") { interaction = new DecorInteraction(catalog, persistence.Baseline); Editing = false; }
                        else if (applied.saved) Editing = false;
                        return Result(applied.status == "activated" ? "Accepted" : "Rejected", applied.diagnosticCode);
                    }
                    finally { Busy = false; }
                case DecorEditAction.RequestDiscard:
                    CancelPreview(); confirmationToken = Guid.NewGuid().ToString("N"); return Result("Accepted");
                default: return Result("Rejected", "ACTION_UNSUPPORTED");
            }
        }
        DecorIntentResult Reload()
        {
            if (confirmationToken != null || Editing && persistence.CanEdit) return Result("Rejected", "DISCARD_OR_APPLY_REQUIRED");
            Busy = true;
            try
            {
                var result = persistence.Reload(prepare);
                if (result.status == "reloaded") { interaction = new DecorInteraction(catalog, persistence.Baseline); Editing = false; CancelPreview(); }
                return Result(result.status == "reloaded" ? "Accepted" : "Rejected", result.diagnosticCode);
            }
            finally { Busy = false; }
        }
        DecorPointerHit Query(DecorPointerRay ray, bool selection = false)
        {
            if (ray == null || !ray.Valid) return new DecorPointerHit { blocked = true, reasonCode = "TRACKED_RAY_REQUIRED" };
            try { return pointer.Query(ray, interaction.Snapshot(), selection ? null : interaction.PreviewItem, Snap) ?? new DecorPointerHit { blocked = true, reasonCode = "TARGET_REQUIRED" }; }
            catch { return new DecorPointerHit { blocked = true, reasonCode = "POINTER_QUERY_FAILED" }; }
        }
        static bool SameTarget(ItemLocation a, ItemLocation b)
        {
            return a.kind == b.kind && a.roomId == b.roomId && a.parentId == b.parentId && a.surfaceId == b.surfaceId &&
                a.yawDeg == b.yawDeg && SameVector(a.positionM, b.positionM) && SameVector(a.localPositionM, b.localPositionM);
        }
        static bool SameVector(double[] a, double[] b)
        { return a == null ? b == null : b != null && a.SequenceEqual(b); }
        public void Dispose() { if (!disposed) { CancelPreview(); persistence.Dispose(); disposed = true; Editing = false; } }
    }
}
