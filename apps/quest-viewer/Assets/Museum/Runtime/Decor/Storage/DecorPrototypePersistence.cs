using System;
using System.IO;

namespace FamilyMuseum.Decor
{
    public interface IDecorPreparedView : IDisposable { void Activate(DecorWorld confirmedSnapshot); }
    public sealed class DecorApplyResult
    {
        public string status, diagnosticCode;
        public bool saved;
    }
    // Coordinates disk and presentation without depending on Unity. Only the store owns durable state.
    public sealed class DecorPrototypePersistence : IDisposable
    {
        readonly string root;
        readonly DecorCatalog catalog;
        readonly DecorWorld fixture;
        DecorWorld baseline;
        DecorWorldStore store;
        bool disposed, busy;
        public string State { get; private set; }
        public string Diagnostic { get; private set; }
        public bool CanEdit { get { return !disposed && store != null && State == "ready"; } }
        public DecorWorld Baseline { get { return baseline == null ? null : DecorCopies.World(baseline); } }
        public DecorPrototypePersistence(string root, DecorCatalog catalog, DecorWorld fixture, Action<DecorStoreStage> fault = null)
        {
            this.root = root; this.catalog = DecorCopies.Catalog(catalog); this.fixture = DecorCopies.World(fixture);
            if (!DecorPlacementValidator.Validate(catalog, fixture).ok) throw new InvalidDataException("FIXTURE_INVALID");
            if (!DecorWorldStore.TryOpen(root, fixture.id, catalog, fault, out store, out var code))
            { Diagnostic = code; ReadCurrent(true); }
            else ReadCurrent(false);
        }
        void ReadCurrent(bool readOnly)
        {
            var loaded = store == null ? DecorWorldStore.ReadOnly(root, fixture.id, catalog) : store.Load();
            if (loaded.status == "notFound") baseline = DecorCopies.World(fixture);
            else if (loaded.status == "loaded" && DecorPlacementValidator.Validate(catalog, loaded.world).ok) baseline = loaded.world;
            else { baseline = null; State = "loadProblem"; Diagnostic = loaded.status == "loaded" ? "SAVED_GEOMETRY_INVALID" : loaded.diagnosticCode ?? loaded.status; return; }
            State = readOnly ? "readOnly" : "ready";
        }
        public DecorApplyResult Apply(DecorWorld draft, Func<DecorWorld, IDecorPreparedView> prepare)
        {
            if (!CanEdit || busy) return Result("blocked", Diagnostic ?? "SESSION_NOT_EDITABLE", false);
            if (!DecorPlacementValidator.Validate(catalog, draft).ok || draft.id != fixture.id || draft.revision != baseline.revision)
                return Result("invalid", "DRAFT_INVALID", false);
            busy = true; IDecorPreparedView prepared = null;
            try
            {
                var candidate = DecorCopies.World(draft);
                try { prepared = prepare(DecorCopies.World(candidate)); if (prepared == null) throw new InvalidOperationException(); }
                catch { return Result("prepareFailed", "PREPARE_FAILED", false); }
                DecorCommitResult committed;
                try { committed = store.Commit(baseline.revision, "batch." + Guid.NewGuid().ToString("N"), candidate); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return Result("saveFailed", "STORE_IO_FAILED", false); }
                if (committed.status != "saved" && committed.status != "alreadySaved") return Result("saveFailed", committed.diagnosticCode, false);
                // After publication, all failures are saved-but-not-activated, never 'save failed'.
                State = "saved-but-not-activated"; Diagnostic = "ACTIVATION_REQUIRED";
                var loaded = store.Load();
                if (loaded.status != "loaded" || loaded.world.revision != committed.revision) return Result(State, Diagnostic, true);
                baseline = loaded.world;
                try { prepared.Activate(DecorCopies.World(baseline)); }
                catch { return Result(State, "ACTIVATION_FAILED", true); }
                State = "ready"; Diagnostic = null; return Result("activated", committed.diagnosticCode, true);
            }
            finally { prepared?.Dispose(); busy = false; }
        }
        public DecorApplyResult Reload(Func<DecorWorld, IDecorPreparedView> prepare)
        {
            if (disposed || busy) return Result("blocked", "SESSION_BUSY", false);
            busy = true; IDecorPreparedView prepared = null;
            try
            {
                bool readOnly = store == null; ReadCurrent(readOnly);
                if (baseline == null) return Result(State, Diagnostic, false);
                // Do not enable old memory on a failed reload.
                State = "saved-but-not-activated";
                try { prepared = prepare(DecorCopies.World(baseline)); prepared.Activate(DecorCopies.World(baseline)); }
                catch { return Result(State, "ACTIVATION_FAILED", true); }
                State = readOnly ? "readOnly" : "ready"; return Result("reloaded", Diagnostic, false);
            }
            finally { prepared?.Dispose(); busy = false; }
        }
        public DecorWorld Cancel() { return Baseline; }
        static DecorApplyResult Result(string state, string code, bool saved) { return new DecorApplyResult { status = state, diagnosticCode = code, saved = saved }; }
        public void Dispose() { if (!disposed) { store?.Dispose(); disposed = true; } }
    }
}
