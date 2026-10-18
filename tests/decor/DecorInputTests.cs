using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyMuseum.Decor;

public static class DecorInputTests
{
    sealed class Scene : IDecorRaycastScene
    {
        public DecorSceneHit[] hits;
        public bool fail;
        public DecorPointerRay received;
        public DecorSceneHit[] Raycast(DecorPointerRay ray) { received = ray; if (fail) throw new IOException(); return hits; }
    }
    sealed class View : IDecorPreparedView
    {
        public DecorWorld activated;
        public bool fail, disposed;
        public void Activate(DecorWorld world) { if (fail) throw new IOException(); activated = world; }
        public void Dispose() { disposed = true; }
    }
    sealed class Harness : IDisposable
    {
        public readonly Scene scene = new Scene();
        public readonly DecorEditorSession session;
        public readonly List<View> views = new List<View>();
        readonly long[] sequences = new long[2];
        public Harness(string root, DecorDocument document, DecorInputSource source = DecorInputSource.PC,
            Func<DecorWorld, IDecorPreparedView> prepare = null)
        {
            session = new DecorEditorSession(root, document.catalog, document.world, new DecorWorldPointer(document.catalog, scene),
                prepare ?? (world => { var view = new View(); views.Add(view); return view; }), source);
        }
        public DecorEditIntent Event(DecorEditAction action, string pointer = "primary")
        {
            return new DecorEditIntent { action = action, sessionToken = session.Input.SessionToken, source = session.Input.Source,
                sequence = ++sequences[(int)session.Input.Source], pointerId = pointer, ray = Ray(),
                phase = action == DecorEditAction.UpdateTarget ? DecorInputPhase.Changed : DecorInputPhase.Performed };
        }
        public DecorIntentResult Send(DecorEditAction action) { return session.Submit(Event(action)); }
        public void Release() { Send(DecorEditAction.ReleaseControls); }
        public void MoveVase(double x)
        {
            var begin = Event(DecorEditAction.BeginMove); begin.instanceId = "vase.001"; session.Submit(begin);
            scene.hits = new[] { Hit(DecorHitRole.Item, 1, x, 1, 0, "plinth.001") };
        }
        public DecorIntentResult Target() { return Send(DecorEditAction.UpdateTarget); }
        public DecorIntentResult Confirm(string token)
        { var intent = Event(DecorEditAction.ConfirmPlacement); intent.candidateToken = token; return session.Submit(intent); }
        public void Dispose() { session.Dispose(); }
    }
    static readonly List<string> results = new List<string>();
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); results.Add("PASS " + name); }
    static DecorPointerRay Ray(double range = 10) { return new DecorPointerRay { tracked = true, originM = new[] { 0d, 2d, 0d }, direction = new[] { 0d, -2d, 0d }, maxDistanceM = range }; }
    static DecorSceneHit Hit(DecorHitRole role, double distance, double x, double y, double z, string item = null, string room = null)
    { return new DecorSceneHit { role = role, distanceM = distance, positionM = new[] { x, y, z }, normal = new[] { 0d, 1d, 0d }, instanceId = item, roomId = room }; }
    static string Json(DecorEditorSession session) { return DecorJson.WriteWorld(session.Snapshot()); }
    public static string[] Run(string fixturePath, string output)
    {
        results.Clear(); Directory.CreateDirectory(output); DecorDocument document; ValidationReport report;
        if (!DecorJson.TryRead(File.ReadAllText(fixturePath), out document, out report)) throw new Exception("Fixture invalid");
        var original = DecorJson.WriteWorld(document.world);
        string pcResult;
        using (var h = new Harness(Path.Combine(output, "pc"), document))
        {
            Check(h.Send(DecorEditAction.Undo).reasonCode == "RELEASE_REQUIRED", "startup requires all controls released");
            h.Release();
            h.MoveVase(.2);
            Check(h.session.HasPreview && !h.session.PreviewValid, "moving old location is not a fresh ray candidate");
            var target = h.Target(); Check(target.status == "Accepted" && target.candidateToken != null, "visible support creates candidate");
            Check(h.scene.received.direction.SequenceEqual(new[] { 0d, -1d, 0d }), "scene query receives normalized world ray");
            var wrongHand = h.Event(DecorEditAction.ConfirmPlacement, "secondary"); wrongHand.candidateToken = target.candidateToken;
            Check(h.session.Submit(wrongHand).reasonCode == "PREVIEW_OWNED", "other hand cannot confirm owned preview");
            var confirm = h.Event(DecorEditAction.ConfirmPlacement); confirm.candidateToken = target.candidateToken;
            Check(h.session.Submit(confirm).commandAccepted && h.session.UndoCount == 1, "one confirmation produces one command");
            Check(h.session.Submit(confirm).reasonCode == "DUPLICATE_INPUT" && h.session.UndoCount == 1, "duplicate event cannot replay command");
            Check(h.session.Snapshot().instances.Single(i => i.id == "vase.001").location.localPositionM[0] == .2, "vase moves by five centimeters");
            var snapshot = Json(h.session); var selected = h.session.SelectedId;
            h.MoveVase(.25); h.Target(); var stale = h.Event(DecorEditAction.ConfirmPlacement);
            Check(h.session.SwitchMode(DecorInputSource.VR), "explicit PC to VR switch succeeds");
            Check(!h.session.HasPreview && Json(h.session) == snapshot && h.session.UndoCount == 1 && h.session.SelectedId == selected, "switch preserves draft selection and Undo, cancels only preview");
            Check(h.session.Submit(stale).reasonCode == "STALE_SESSION", "queued PC event rejected after switch");
            Check(h.Send(DecorEditAction.Undo).reasonCode == "RELEASE_REQUIRED", "mode switch requires release before command");
            h.Release();
            var inactive = h.Event(DecorEditAction.Undo); inactive.source = DecorInputSource.PC;
            Check(h.session.Submit(inactive).reasonCode == "INACTIVE_SOURCE", "inactive PC adapter cannot edit VR session");
            Check(h.Send(DecorEditAction.Undo).commandAccepted && h.session.RedoCount == 1, "VR undo uses same PC history");
            Check(h.session.SwitchMode(DecorInputSource.PC), "VR to PC switch succeeds"); h.Release();
            Check(h.Send(DecorEditAction.Redo).commandAccepted && Json(h.session) == snapshot, "PC redo restores same vase and location");
            var detached = h.session.Snapshot(); detached.instances[0].variantId = "bad";
            Check(Json(h.session) == snapshot, "view snapshot cannot mutate session");
            var presentation = DecorEditorPresenter.Read(h.session); presentation.selectedId = "bad";
            Check(h.session.SelectedId == selected, "presenter returns detached view state");
            Check(presentation.canApply && presentation.canUndo && presentation.canRotate && presentation.canMove, "common presenter owns action availability for both views");
            using (var second = new Harness(Path.Combine(output, "pc"), document))
            { second.Release(); Check(second.session.State == "readOnly" && !second.session.CanEdit, "view switches do not release writer lock");
                Check(second.Send(DecorEditAction.BeginPlace).reasonCode == "SESSION_NOT_EDITABLE", "second writer cannot create items"); }
            h.MoveVase(.25); var candidate = h.Target();
            h.scene.hits = new[] { Hit(DecorHitRole.Solid, .1, 0, 1, 0), Hit(DecorHitRole.Item, 1, .25, 1, 0, "plinth.001") };
            Check(h.Confirm(candidate.candidateToken).reasonCode == "TARGET_OCCLUDED" && Json(h.session) == snapshot, "confirmation re-queries new obstruction, no stale placement");
            Check(!h.session.PreviewValid, "obstruction invalidates preview");
            h.Send(DecorEditAction.CancelPreview);
            h.MoveVase(.25); candidate = h.Target(); // blocked candidate
            h.scene.hits = new[] { Hit(DecorHitRole.Item, 1, .25, 1, 0, "plinth.001") }; candidate = h.Target();
            h.scene.hits[0].positionM[0] = .3;
            Check(h.Confirm(candidate.candidateToken).reasonCode == "TARGET_CHANGED" && h.session.UndoCount == 1, "different confirmation target requires new preview token");
            h.session.Suspend(); Check(!h.session.HasPreview && Json(h.session) == snapshot, "tracking/focus suspension cancels preview without changing draft");
            Check(h.Send(DecorEditAction.Undo).reasonCode == "INPUT_SUSPENDED", "suspended session rejects commands");
            h.session.Resume(); h.Release();
            var overUi = h.Event(DecorEditAction.Undo);
            Check(h.session.Submit(overUi, true).reasonCode == "UI_CONSUMED" && h.session.UndoCount == 1, "UI consumption blocks world command");
            Check(h.session.Submit(overUi).reasonCode == "DUPLICATE_INPUT", "UI-consumed event cannot be replayed into world");
            Check(h.session.Submit(h.Event(DecorEditAction.Undo), false, true).reasonCode == "TEXT_EDITING", "text focus blocks edit shortcuts");
            var phase = h.Event(DecorEditAction.Undo); phase.phase = DecorInputPhase.Started;
            Check(h.session.Submit(phase).reasonCode == "PHASE_INVALID", "started phase cannot perform command");
            Check(h.Send(DecorEditAction.Reload).reasonCode == "DISCARD_OR_APPLY_REQUIRED" && Json(h.session) == snapshot, "reload cannot silently discard active draft");
            var discard = h.Send(DecorEditAction.RequestDiscard);
            Check(!h.session.CanEdit && discard.confirmationToken != null, "discard request opens common modal gate");
            Check(h.Send(DecorEditAction.Undo).reasonCode == "SESSION_NOT_EDITABLE", "discard modal blocks editing");
            h.Send(DecorEditAction.ResumeEdit);
            var oldConfirm = h.Event(DecorEditAction.ConfirmDiscard); oldConfirm.confirmationToken = discard.confirmationToken;
            Check(h.session.Submit(oldConfirm).reasonCode == "STALE_CONFIRMATION" && Json(h.session) == snapshot, "dismissed discard confirmation cannot be replayed");
            pcResult = Json(h.session);
            Check(h.Send(DecorEditAction.Apply).status == "Accepted" && !h.session.Editing, "common apply saves and exits editing");
            Check(h.views.Count == 1 && h.views[0].activated.revision == 1 && h.views[0].disposed, "prepare activate dispose use confirmed saved revision");
            Check(!h.session.CanEdit && h.session.Snapshot().revision == 1, "post-save state uses confirmed baseline");
        }
        using (var h = new Harness(Path.Combine(output, "pc"), document, DecorInputSource.VR))
        { h.Release(); Check(h.session.Snapshot().revision == 1 && h.session.Snapshot().instances.Single(i => i.id == "vase.001").location.localPositionM[0] == .2, "fresh VR session loads PC-saved vase without separate store"); }
        using (var h = new Harness(Path.Combine(output, "vr"), document, DecorInputSource.VR))
        {
            h.Release(); h.MoveVase(.2); var candidate = h.Target(); Check(h.Confirm(candidate.candidateToken).commandAccepted, "VR source follows same placement command path");
            Check(Json(h.session) == pcResult && h.session.UndoCount == 1, "PC and VR deterministic candidates yield identical world and history");
            var sharedSequence = h.Event(DecorEditAction.RotateStep, "primary"); sharedSequence.direction = 1;
            Check(h.session.Submit(sharedSequence).commandAccepted, "VR primary performs selected rotation once");
            sharedSequence.pointerId = "secondary";
            Check(h.session.Submit(sharedSequence).reasonCode == "DUPLICATE_INPUT" && h.session.UndoCount == 2, "VR hands share source-wide deduplication sequence");
            h.Send(DecorEditAction.Undo);
            var before = Json(h.session); h.MoveVase(.25); h.Target();
            Check(h.Send(DecorEditAction.Apply).reasonCode == "PREVIEW_ACTIVE" && Json(h.session) == before, "cannot save an unconfirmed preview");
            var request = h.Send(DecorEditAction.RequestDiscard); var confirm = h.Event(DecorEditAction.ConfirmDiscard); confirm.confirmationToken = request.confirmationToken;
            Check(h.session.Submit(confirm).status == "Accepted" && Json(h.session) == original && h.session.UndoCount == 0, "explicit discard restores baseline only after matching token");
        }
        using (var h = new Harness(Path.Combine(output, "prepare-failure"), document, prepare: world => { throw new IOException(); }))
        {
            h.Release(); h.MoveVase(.2); h.Confirm(h.Target().candidateToken); var before = Json(h.session);
            Check(h.Send(DecorEditAction.Apply).reasonCode == "PREPARE_FAILED" && Json(h.session) == before && h.session.UndoCount == 1 && h.session.CanEdit, "prepare failure preserves draft history and editing");
        }
        bool failActivation = true;
        using (var h = new Harness(Path.Combine(output, "activate-failure"), document, prepare: world => new View { fail = failActivation }))
        {
            h.Release(); h.MoveVase(.2); h.Confirm(h.Target().candidateToken);
            Check(h.Send(DecorEditAction.Apply).reasonCode == "ACTIVATION_FAILED" && h.session.State == "saved-but-not-activated" && !h.session.CanEdit, "activation failure reports durable save and blocks old world edits");
            Check(h.session.SwitchMode(DecorInputSource.VR), "recovery state supports explicit mode switch"); h.Release();
            Check(h.Send(DecorEditAction.BeginMove).reasonCode == "SESSION_NOT_EDITABLE", "mode change cannot bypass saved-but-not-activated gate");
            failActivation = false;
            Check(h.Send(DecorEditAction.Reload).status == "Accepted" && h.session.Snapshot().revision == 1 && !h.session.Editing, "recovery reload activates saved revision without another commit");
            Check(h.Send(DecorEditAction.ResumeEdit).status == "Accepted" && h.session.CanEdit, "editing resumes explicitly after recovered activation");
        }
        DecorEditorSession busySession = null; bool switchBlocked = false;
        using (var h = new Harness(Path.Combine(output, "busy"), document, prepare: world => { switchBlocked = !busySession.SwitchMode(DecorInputSource.VR); return new View(); }))
        { busySession = h.session; h.Release(); Check(h.Send(DecorEditAction.Apply).status == "Accepted" && switchBlocked, "view activation cannot switch mode during save"); }
        using (var h = new Harness(Path.Combine(output, "queries"), document))
        {
            h.Release(); var begin = h.Event(DecorEditAction.BeginPlace); begin.definitionId = "core.plinth.small"; begin.definitionVersion = 1;
            h.session.Submit(begin);
            h.scene.hits = new[] { Hit(DecorHitRole.Floor, 2, 3, 0, 0, room: "room.a"), Hit(DecorHitRole.Solid, 1, 1, 1, 0) };
            Check(h.Target().reasonCode == "TARGET_OCCLUDED", "wall before empty floor blocks candidate regardless of hit ordering");
            h.scene.hits[1].role = DecorHitRole.Item; h.scene.hits[1].instanceId = "plinth.001";
            Check(h.Target().reasonCode == "VISIBLE_FLOOR_REQUIRED", "furniture before empty floor blocks floor placement");
            h.scene.hits[1].role = DecorHitRole.UI;
            Check(h.Target().reasonCode == "TARGET_OCCLUDED", "world UI blocker prevents placement behind panel");
            h.scene.hits[1].role = DecorHitRole.PlayerProxy;
            Check(h.Target().status == "Accepted", "explicit player proxy excluded from world obstruction query");
            h.scene.hits[1].role = DecorHitRole.Preview;
            Check(h.Target().status == "Accepted", "preview collider excluded from world query");
            var shortRay = h.Event(DecorEditAction.UpdateTarget); shortRay.ray.maxDistanceM = .5;
            Check(h.session.Submit(shortRay).reasonCode == "TARGET_REQUIRED", "targets outside maximum ray distance excluded");
            var untracked = h.Event(DecorEditAction.UpdateTarget); untracked.ray.tracked = false;
            Check(h.session.Submit(untracked).reasonCode == "TRACKED_RAY_REQUIRED", "untracked ray cannot update target");
            h.scene.fail = true; Check(h.Target().reasonCode == "POINTER_QUERY_FAILED" && !h.session.PreviewValid, "query failure invalidates previous candidate"); h.scene.fail = false;
            h.scene.hits = new[] { Hit(DecorHitRole.Floor, 2, 3.13, 0, 0, room: "room.a") };
            Check(h.Target().status == "Accepted" && h.session.PreviewItem.location.positionM[0] == 3.25, "floor target uses common 25cm snapping");
            var beforeRotation = h.Target(); var rotate = h.Event(DecorEditAction.RotateStep); rotate.direction = 1;
            Check(h.session.Submit(rotate).status == "Accepted" && h.session.UndoCount == 0 && h.session.PreviewItem.location.yawDeg == 90, "preview rotation changes presentation without history");
            Check(h.Confirm(beforeRotation.candidateToken).reasonCode == "STALE_CANDIDATE", "rotation invalidates old candidate token");
            var snapOff = h.Event(DecorEditAction.SetSnap); snapOff.enabled = false; h.session.Submit(snapOff);
            Check(h.Target().status == "Accepted" && h.session.PreviewItem.location.positionM[0] == 3.13, "free position uses shared millimeter quantization");
            h.scene.hits = new[] { Hit(DecorHitRole.Item, 1, 0, 1, 0, "plinth.001") };
            Check(h.Send(DecorEditAction.SampleDefinition).status == "Accepted" && h.session.PreviewItem.definition.id == "core.plinth.small" && h.session.UndoCount == 0, "eyedropper while previewing queries selection without placement filtering");
            var invalid = h.Event(DecorEditAction.RotateStep); invalid.direction = 2;
            Check(h.session.Submit(invalid).reasonCode == "DIRECTION_INVALID", "invalid quarter-turn payload rejected");
            h.session.Dispose(); Check(h.Send(DecorEditAction.Undo).reasonCode == "SESSION_CLOSED", "disposed session rejects input");
        }
        Check(DecorJson.WriteWorld(document.world) == original, "caller fixture remains unchanged");
        results.Add("DECOR_INPUT_PASS checks=" + results.Count + " (production C#, simulated scene hits; no Unity/XR device validation)");
        return results.ToArray();
    }
}
