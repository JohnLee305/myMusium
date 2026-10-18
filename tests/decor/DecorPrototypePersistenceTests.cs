using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyMuseum.Decor;

public static class DecorPrototypePersistenceTests
{
    sealed class View : IDecorPreparedView
    {
        internal bool active, disposed, fail;
        internal DecorWorld world;
        public void Activate(DecorWorld saved) { if (fail) throw new InvalidOperationException("activation"); active = true; world = saved; }
        public void Dispose() { disposed = true; }
    }
    public static string[] Run(string fixtureFile, string root)
    {
        var results = new List<string>(); Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("FAIL: " + name); results.Add("PASS: " + name); };
        DecorDocument d; ValidationReport report; DecorJson.TryRead(File.ReadAllText(fixtureFile), out d, out report);
        var source = DecorJson.WriteWorld(d.world); string file = Path.Combine(root, d.world.id, "world.json");
        var p = new DecorPrototypePersistence(root, d.catalog, d.world);
        check(p.CanEdit && p.State == "ready" && p.Baseline.revision == 0, "new prototype opens without publishing initial world");
        var before = p.Baseline; before.instances[0].location.positionM[0] = 99;
        check(p.Baseline.instances[0].location.positionM[0] == 0, "baseline getter is a defensive copy");
        int prepareCalls = 0; var invalid = p.Baseline; invalid.instances[0].location.positionM[0] = 99;
        var r = p.Apply(invalid, w => { prepareCalls++; return new View(); });
        check(r.status == "invalid" && prepareCalls == 0 && !File.Exists(file), "geometry validation precedes inactive preparation and disk publication");
        r = p.Apply(p.Baseline, w => { throw new IOException("prepare"); });
        check(r.status == "prepareFailed" && !r.saved && !File.Exists(file) && p.CanEdit, "preparation failure is unsaved and leaves baseline editable");
        var draft = p.Baseline; draft.instances[0].location.positionM = new double[] { 3, 0, 2 }; draft.instances[0].location.yawDeg = 90; draft.instances[0].variantId = "ivory";
        var prepared = new View(); bool diskExistedAtPreparation = true;
        r = p.Apply(draft, w => { diskExistedAtPreparation = File.Exists(file); return prepared; });
        check(r.status == "activated" && r.saved && !diskExistedAtPreparation, "prepare completes before first disk commit then activation");
        check(prepared.active && prepared.disposed && prepared.world.revision == 1, "activation receives confirmed revision and prepared resource is disposed");
        check(File.ReadAllText(file) == DecorJson.WriteWorld(p.Baseline), "active baseline equals actual durable snapshot");
        check(p.Baseline.instances[1].id == "vase.001" && p.Baseline.instances[1].location.parentId == "plinth.001" && p.Baseline.instances[0].variantId == "ivory", "save preserves IDs, attachment, color, movement and rotation");
        byte[] bytes = File.ReadAllBytes(file); var cancel = p.Cancel(); cancel.instances[0].location.positionM[0] = -5;
        check(File.ReadAllBytes(file).SequenceEqual(bytes) && p.Cancel().instances[0].location.positionM[0] == 3, "whole cancel preserves disk bytes and original baseline meaning");
        using (var second = new DecorPrototypePersistence(root, d.catalog, d.world))
        {
            check(second.State == "readOnly" && second.Diagnostic == "WORLD_IN_USE" && !second.CanEdit, "simultaneous writer opens as explicit read-only");
            check(DecorJson.WriteWorld(second.Baseline) == File.ReadAllText(file), "read-only open displays actual saved content");
            check(second.Apply(second.Baseline, w => new View()).status == "blocked" && File.ReadAllBytes(file).SequenceEqual(bytes), "read-only session cannot publish changes");
        }
        draft = p.Baseline; draft.instances[0].location.positionM[0] = 4; prepared = new View { fail = true };
        r = p.Apply(draft, w => prepared);
        check(r.saved && r.status == "saved-but-not-activated" && p.State == r.status && !p.CanEdit, "post-save activation failure blocks stale memory and reports saved");
        check(prepared.disposed && !prepared.active && p.Baseline.revision == 2, "failed activation disposes prepared runtime and preserves confirmed revision");
        bytes = File.ReadAllBytes(file);
        check(p.Apply(draft, w => new View()).status == "blocked", "editing cannot resume through another Apply after activation failure");
        prepared = new View(); r = p.Reload(w => prepared);
        check(r.status == "reloaded" && p.CanEdit && prepared.active && prepared.world.revision == 2 && File.ReadAllBytes(file).SequenceEqual(bytes), "explicit reload activates saved state without a second commit");
        p.Dispose();
        using (var reopened = new DecorPrototypePersistence(root, d.catalog, d.world))
            check(reopened.CanEdit && DecorJson.WriteWorld(reopened.Baseline) == File.ReadAllText(file), "new session restores exact durable state after writer exits");
        using (var fault = new DecorPrototypePersistence(root, d.catalog, d.world, stage => { if (stage == DecorStoreStage.BeforeWrite) throw new IOException("disk"); }))
        {
            draft = fault.Baseline; draft.instances[0].location.positionM[0] = 5; prepared = new View(); r = fault.Apply(draft, w => prepared);
            check(!r.saved && r.status == "saveFailed" && fault.CanEdit && !prepared.active && prepared.disposed, "disk failure keeps old memory and releases inactive runtime");
            check(File.ReadAllBytes(file).SequenceEqual(bytes) && fault.Baseline.revision == 2, "disk failure preserves exact original bytes and revision");
        }
        using (var fault = new DecorPrototypePersistence(root, d.catalog, d.world, stage => { if (stage == DecorStoreStage.AfterReplace) throw new IOException("after replace"); }))
        {
            draft = fault.Baseline; draft.instances[0].location.positionM[0] = 5; r = fault.Apply(draft, w => new View());
            check(r.saved && r.status == "activated" && r.diagnosticCode == "POST_SAVE_WARNING" && fault.Baseline.revision == 3, "post-replace warning cannot be reported as unsaved");
        }
        File.WriteAllText(file, "{broken"); bytes = File.ReadAllBytes(file);
        using (var broken = new DecorPrototypePersistence(root, d.catalog, d.world))
        { check(broken.State == "loadProblem" && !broken.CanEdit && broken.Baseline == null, "corrupt save never falls back to an editable fixture"); check(broken.Apply(d.world, w => new View()).status == "blocked" && File.ReadAllBytes(file).SequenceEqual(bytes), "corrupt evidence cannot be erased by Apply"); }
        check(DecorJson.WriteWorld(d.world) == source, "caller fixture never mutated by save, reload or cancel");
        results.Add("DECOR_PROTOTYPE_PASS checks=" + results.Count); return results.ToArray();
    }
}
