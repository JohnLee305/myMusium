using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace FamilyMuseum.Decor
{
    // Explicit diagnostic flag only. Runs the real Player input and UI pipeline with synthetic devices.
    public sealed class DecorPrototypeSmoke : MonoBehaviour
    {
        DecorPanel panel; string output; Keyboard keyboard; Mouse mouse; int checks;
        readonly List<string> results = new List<string>(); bool failed;
        public void Begin(DecorPanel value, string path)
        {
            panel = value; output = path; Directory.CreateDirectory(path);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            StartCoroutine(Run());
        }
        void Check(bool ok, string name)
        { results.Add((ok ? "PASS " : "FAIL ") + name); checks++; if (!ok) failed = true; Debug.Log(results[results.Count - 1]); }
        IEnumerator Pause() { yield return null; yield return null; yield return new WaitForEndOfFrame(); }
        Vector2 ScreenPoint(VisualElement e)
        { var p = e.worldBound.center; var r = panel.Root.worldBound; return new Vector2(p.x / r.width * Screen.width, Screen.height - p.y / r.height * Screen.height); }
        void MouseState(Vector2 position, bool left = false, Vector2 scroll = default)
        { var state = new MouseState { position = position, scroll = scroll }; if (left) state = state.WithButton(MouseButton.Left); InputSystem.QueueStateEvent(mouse, state); }
        IEnumerator Click(VisualElement e)
        { var p = ScreenPoint(e); MouseState(p); yield return Pause(); MouseState(p, true); yield return Pause(); MouseState(p); yield return Pause(); }
        IEnumerator KeyState(params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return Pause(); }
        IEnumerator Capture(string file)
        {
            Texture2D texture = null; bool visible = false;
            for (int i = 0; i < 12 && !visible; i++)
            {
                if (texture != null) Destroy(texture); yield return Pause();
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                visible = texture.GetPixels32().Where((p, n) => n % 997 == 0).Count(p => p.r + p.g + p.b > 90) > 20;
            }
            Check(visible, file + " actual rendered frame is nonblank");
            File.WriteAllBytes(Path.Combine(output, file + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        IEnumerator Drag(MouseButton button, Vector2 from, Vector2 to)
        {
            MouseState(from); yield return Pause(); InputSystem.QueueStateEvent(mouse, new MouseState { position = from }.WithButton(button)); yield return Pause();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = to, delta = to - from }.WithButton(button)); yield return Pause();
            MouseState(to); yield return Pause();
        }
        IEnumerator Run()
        {
            keyboard = InputSystem.AddDevice<Keyboard>("DecorSmokeKeyboard"); mouse = InputSystem.AddDevice<Mouse>("DecorSmokeMouse");
            GetComponent<DecorInputRouter>().UseDiagnosticDevices(keyboard, mouse);
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            var settings = GetComponent<UIDocument>().panelSettings;
            Check(FindObjectsByType<FamilyMuseum.DesktopNavigator>(FindObjectsSortMode.None).Length == 0 && FindObjectsByType<FamilyMuseum.MuseumSession>(FindObjectsSortMode.None).Length == 0, "prototype isolates viewing components");
            var documentBefore = DecorJson.Write(panel.Document);
            int[,] sizes = { { 1280, 800 }, { 1920, 1080 }, { 1024, 720 }, { 1280, 800 }, { 1280, 800 }, { 1024, 720 } };
            float[] scales = { 1, 1, 1, 1.25f, 1.5f, 1.5f };
            for (int i = 0; i < scales.Length; i++)
            {
                Screen.SetResolution(sizes[i, 0], sizes[i, 1], FullScreenMode.Windowed); settings.scale = scales[i];
                yield return new WaitForSecondsRealtime(.7f); yield return Pause();
                var root = panel.Root.worldBound; var top = panel.Root.Q("toolbar").worldBound; var catalog = panel.Root.Q("catalog").worldBound; var viewport = panel.Root.Q("viewport").worldBound; var status = panel.Root.Q("status").worldBound;
                string size = sizes[i, 0] + "x" + sizes[i, 1] + "-" + scales[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                Check(Mathf.Abs(Screen.width - sizes[i, 0]) < 2 && Mathf.Abs(Screen.height - sizes[i, 1]) < 2, size + " actual Player resolution");
                Check(top.yMax <= viewport.yMin + 1 && viewport.yMax <= catalog.yMin + 1 && catalog.yMax <= status.yMin + 1 && status.yMax <= root.yMax + 1 && viewport.height >= 63, size + " main regions fit without overlap");
                var toolbarButtons = panel.Root.Q("toolbar").Query<Button>().ToList().Where(b => b.resolvedStyle.display != DisplayStyle.None).ToArray();
                bool overlap = false;
                for (int a = 0; a < toolbarButtons.Length; a++)
                { var rect = toolbarButtons[a].worldBound; if (rect.xMin < root.xMin || rect.xMax > root.xMax + 1 || rect.yMax > top.yMax) overlap = true; for (int b = a + 1; b < toolbarButtons.Length; b++) if (rect.Overlaps(toolbarButtons[b].worldBound)) overlap = true; }
                Check(!overlap, size + " toolbar buttons remain separate and inside window");
                Check(panel.Root.Q<Button>("collection-tab").enabledSelf == false && panel.Root.Q<Button>("space-tab").enabledSelf == false, size + " future tabs visibly disabled");
                var names = panel.Root.Query<Label>(className: "card-name").ToList();
                Check(names.Any(l => l.text.Contains("소품을")) && names.All(l => l.resolvedStyle.whiteSpace == WhiteSpace.Normal && l.worldBound.height >= 40), size + " Korean names have two-line space");
                var cardViewport = panel.Root.Q<ScrollView>("cards").contentViewport.worldBound;
                var subtitleBottom = panel.Root.Query<Label>(className: "card-subtitle").ToList().Max(l => l.worldBound.yMax);
                Check(subtitleBottom <= cardViewport.yMax + 1, size + " card descriptions clear scrollbar (bottom=" + subtitleBottom + ", viewport=" + cardViewport.yMax + ")");
                yield return Capture(size);
            }
            settings.scale = 1; Screen.SetResolution(1280, 800, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.6f); yield return Pause();
            var search = panel.Root.Q<TextField>("search"); yield return Click(search);
            Check(panel.TextEditing, "actual mouse click focuses search");
            var pos = panel.cameraRig.Pivot; float yaw = panel.cameraRig.Yaw; float distance = panel.cameraRig.Distance;
            yield return KeyState(Key.W, Key.A, Key.Q, Key.E, Key.F, Key.G); yield return new WaitForSecondsRealtime(.15f);
            Check(Vector3.Distance(pos, panel.cameraRig.Pivot) < .0001f && Mathf.Abs(yaw - panel.cameraRig.Yaw) < .0001f && Mathf.Abs(distance - panel.cameraRig.Distance) < .0001f, "WASD/QE/F/G in search cannot move camera");
            yield return KeyState(); search.value = "꽃병"; yield return Pause();
            Check(panel.Root.Query<Button>(className: "card").ToList().Count == 1, "Korean search filters actual cards");
            search.value = "없는물건"; yield return Pause(); Check(panel.Root.Query<Button>(className: "card").ToList().Count == 0, "empty search has no false cards");
            search.value = ""; search.Blur(); panel.Root.focusController.focusedElement?.Blur(); yield return Pause();
            var card = panel.Root.Query<Button>(className: "card").First(); pos = panel.cameraRig.Pivot; distance = panel.cameraRig.Distance;
            yield return Click(card); Check(panel.SelectedInstanceId == null && Vector3.Distance(pos, panel.cameraRig.Pivot) < .0001f, "UI catalog click does not also select world or move camera");
            // At 1280 all eight sample cards can fit. Use an actual overflowing viewport for the scroll assertion.
            Screen.SetResolution(1024, 720, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.5f); yield return Pause();
            var scroll = panel.Root.Q<ScrollView>("cards"); var wheelPoint = ScreenPoint(scroll); MouseState(wheelPoint, false, new Vector2(0, -120)); yield return Pause(); MouseState(wheelPoint); yield return Pause();
            Check(Mathf.Abs(distance - panel.cameraRig.Distance) < .0001f, "wheel over UI does not zoom camera");
            Check(scroll.scrollOffset.x > 0, "wheel actually scrolls horizontal catalog");
            Screen.SetResolution(1280, 800, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.5f); yield return Pause();
            yield return Click(panel.Root.Q<Button>("inventory-tab"));
            Check(panel.Root.Query<Button>(className: "card").ToList().Count == 1 && panel.Root.Query<Label>(className: "card-subtitle").First().text.Contains("소품 포함"), "inventory displays actual stored assembly count");
            yield return Click(panel.Root.Q<Button>("catalog-tab"));
            panel.Editor.Cancel();
            var target = FindObjectsByType<DecorSelectable>(FindObjectsSortMode.None).First(o => o.InstanceId == "plinth.001");
            var worldPoint = panel.cameraRig.View.WorldToScreenPoint(target.GetComponentInChildren<Renderer>().bounds.center);
            Check(!panel.IsOverUI(worldPoint), "world pointer is not intercepted by transparent UI root");
            MouseState(worldPoint); yield return Pause(); MouseState(worldPoint, true); yield return Pause(); MouseState(worldPoint); yield return Pause();
            Check(panel.SelectedInstanceId == "plinth.001", "actual world click selects plinth");
            var dragPoint = ScreenPoint(panel.Root.Q("viewport")) + new Vector2(-100, 0); yaw = panel.cameraRig.Yaw;
            yield return Drag(MouseButton.Right, dragPoint, dragPoint + new Vector2(90, 20));
            Check(Mathf.Abs(panel.cameraRig.Yaw - yaw) > 5, "right drag orbits camera");
            pos = panel.cameraRig.Pivot; yield return Drag(MouseButton.Middle, dragPoint, dragPoint + new Vector2(50, 20));
            Check(Vector3.Distance(pos, panel.cameraRig.Pivot) > .1f, "middle drag pans camera");
            pos = panel.cameraRig.Pivot; yield return Drag(MouseButton.Middle, ScreenPoint(panel.Root.Q("toolbar")), dragPoint);
            Check(Vector3.Distance(pos, panel.cameraRig.Pivot) < .0001f, "drag beginning on UI never becomes world pan");
            pos = panel.cameraRig.Pivot; yield return KeyState(Key.W); yield return new WaitForSecondsRealtime(.15f); yield return KeyState();
            Check(Vector3.Distance(pos, panel.cameraRig.Pivot) > .01f, "camera movement works outside text editing");
            distance = panel.cameraRig.Distance; MouseState(new Vector2(Screen.width * .35f, Screen.height * .55f), false, new Vector2(0, 120)); yield return Pause(); MouseState(new Vector2(Screen.width * .35f, Screen.height * .55f)); yield return Pause();
            Check(panel.cameraRig.Distance < distance, "world wheel zoom works (before=" + distance + ", after=" + panel.cameraRig.Distance + ")");
            yield return KeyState(Key.F); yield return KeyState(); Check(panel.cameraRig.Distance < 4, "F frames selected item");
            panel.SelectInstance("vase.001"); yield return Pause();
            Check(panel.Root.Q("parent").resolvedStyle.display == DisplayStyle.Flex && panel.Root.Q<Label>("selection-body").text.Contains("받침대 위"), "vase details expose parent selection");
            yield return Click(panel.Root.Q<Button>("parent")); Check(panel.SelectedInstanceId == "plinth.001", "parent button selects support instead of child");
            panel.FrameRoom(); panel.CycleWalls(); panel.CycleWalls(); panel.ToggleGrid(); panel.ToggleGrid();
            Check(DecorJson.Write(panel.Document) == documentBefore, "viewing selections/filter/walls/grid/camera never mutate fixture world");
            yield return InteractionChecks();
            yield return PersistenceChecks();
            panel.SelectInstance("plinth.001"); panel.FrameSelection(); yield return Pause();
            yield return Capture("selected-item");
            File.WriteAllLines(Path.Combine(output, "results.txt"), results.Concat(new[] { (failed ? "DECOR_UI_FAIL" : "DECOR_UI_PASS") + " checks=" + checks }));
            File.WriteAllText(Path.Combine(output, "ready-for-concurrent.txt"), "ready");
            float deadline = Time.realtimeSinceStartup + 45;
            while (!File.Exists(Path.Combine(output, "concurrent-done.txt")) && Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log((failed ? "DECOR_UI_FAIL" : "DECOR_UI_PASS") + " checks=" + checks);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); yield return new WaitForSecondsRealtime(.3f); Application.Quit(failed ? 1 : 0);
        }
        IEnumerator PersistenceChecks()
        {
            var session = panel.Session; var target = Path.Combine(session.StoreRoot, panel.Document.world.id, "world.json");
            session.FailPrepareOnce = true; yield return Click(panel.Root.Q<Button>("apply"));
            Check(!File.Exists(target) && session.Editing && panel.Root.Q<Label>("status").text.Contains("저장하지 않았"), "prepare failure never publishes snapshot and keeps editing");
            Directory.CreateDirectory(target); yield return Click(panel.Root.Q<Button>("apply"));
            Check(!File.Exists(target) && session.Editing && panel.Root.Q<Label>("status").text.Contains("저장하지 못"), "real disk failure preserves draft and shows unsaved message");
            Directory.Delete(target);
            yield return KeyState(Key.LeftCtrl, Key.S); yield return KeyState();
            Check(File.Exists(target) && !session.Editing && !session.Modal && session.Persistence.State == "ready", "Ctrl S validates, prepares, saves and activates viewing mode");
            var bytes = File.ReadAllBytes(target); string saved = DecorJson.WriteWorld(panel.Document.world); int revision = panel.Document.world.revision;
            Check(saved == File.ReadAllText(target), "activated scene snapshot exactly matches durable content");
            yield return Click(panel.Root.Q<Button>("edit-again"));
            string chosen = panel.Document.world.instances.First(i => i.location.kind == "room" && i.variantId == "charcoal").id;
            panel.SelectInstance(chosen); yield return KeyState(Key.E); yield return KeyState();
            string draft = DecorJson.WriteWorld(panel.Editor.Interaction.Snapshot());
            Check(draft != saved && File.ReadAllBytes(target).SequenceEqual(bytes), "editing changes stay in draft until Apply");
            yield return Click(panel.Root.Q<Button>("cancel-edit"));
            Check(session.Modal && panel.Root.Q("discard-confirm").resolvedStyle.display == DisplayStyle.Flex, "whole edit cancel asks before discarding changed draft");
            yield return Click(panel.Root.Q<Button>("keep-editing"));
            Check(!session.Modal && draft == DecorJson.WriteWorld(panel.Editor.Interaction.Snapshot()), "keep editing preserves all draft changes");
            yield return Click(panel.Root.Q<Button>("cancel-edit")); yield return Click(panel.Root.Q<Button>("discard-confirm"));
            Check(!session.Editing && saved == DecorJson.WriteWorld(panel.Document.world) && File.ReadAllBytes(target).SequenceEqual(bytes), "confirmed whole cancel restores original content and leaves disk byte-identical");
            yield return Click(panel.Root.Q<Button>("edit-again")); panel.SelectInstance(chosen); yield return KeyState(Key.E); yield return KeyState();
            session.FailActivateOnce = true; yield return Click(panel.Root.Q<Button>("apply"));
            Check(session.Modal && session.Persistence.State == "saved-but-not-activated" && panel.Root.Q<Label>("session-message").text.Contains("저장됐지만"), "post-save activation failure explicitly reports saved-but-not-activated");
            var loaded = DecorWorldStore.ReadOnly(session.StoreRoot, panel.Document.world.id, panel.Document.catalog);
            Check(loaded.status == "loaded" && loaded.world.revision == revision + 1 && !session.CanEdit, "activation failure still has newer durable snapshot and blocks stale edits");
            yield return Capture("saved-activation-problem");
            yield return Click(panel.Root.Q<Button>("reload-saved"));
            Check(!session.Modal && !session.Editing && panel.Document.world.revision == revision + 1 && DecorJson.WriteWorld(panel.Document.world) == File.ReadAllText(target), "reload activates actual saved snapshot without a second commit");
            File.WriteAllText(Path.Combine(output, "expected-world.json"), DecorJson.WriteWorld(panel.Document.world));
            yield return Capture("saved-world");
        }
        IEnumerator WorldClick(Vector3 position, bool shift = false, bool alt = false)
        {
            if (shift) yield return KeyState(Key.LeftShift);
            if (alt) yield return KeyState(Key.LeftAlt);
            var point = (Vector2)panel.cameraRig.View.WorldToScreenPoint(position);
            MouseState(point); yield return Pause(); MouseState(point, true); yield return Pause(); MouseState(point); yield return Pause(); yield return KeyState();
        }
        IEnumerator InteractionChecks()
        {
            var edit = panel.Editor.Interaction;
            panel.ClearSelection(); panel.Editor.Cancel(); panel.cameraRig.Frame(new Bounds(new Vector3(0, .5f, 0), new Vector3(10, 2, 8)));
            // Cutaway must not put a wall between camera and the chosen placement surface.
            panel.CycleWalls(); panel.CycleWalls(); // previous test left all walls; now hidden
            yield return Pause();
            var card = panel.Root.Query<Button>(className: "card").First(); yield return Click(card);
            Check(edit.HasPreview && edit.UndoCount == 0, "catalog click starts preview without command");
            int count = edit.Inventory().ownedCount;
            yield return WorldClick(new Vector3(-1.4f, 0, 0));
            Check(edit.HasPreview && !edit.PreviewValid && edit.UndoCount == 0 && edit.Inventory().ownedCount == count, "invalid overlapping click preserves ownership and Undo");
            yield return Capture("rejected-placement");
            yield return WorldClick(new Vector3(-3, 0, -2));
            string newId = panel.SelectedInstanceId;
            Check(!edit.HasPreview && edit.UndoCount == 1 && edit.Inventory().ownedCount == count + 1 && newId != null, "real mouse places exactly one new plinth");
            yield return Click(panel.Root.Q("swatches").Children().OfType<Button>().Last());
            Check(panel.Document.world.instances.First(i => i.id == newId).variantId == "charcoal", "actual color chip submits selected variant");
            yield return KeyState(Key.E); yield return KeyState();
            Check(panel.Document.world.instances.First(i => i.id == newId).location.yawDeg == 90, "E rotates selected furniture by quarter turn");
            panel.Editor.Cancel(); // clear selection so the next catalog click has no side effects
            yield return Click(panel.Root.Query<Button>(className: "card").ToList()[1]);
            var plinth = panel.ObjectFor(newId); var top = plinth.transform.TransformPoint(new Vector3(0, 1, 0));
            panel.cameraRig.Frame(new Bounds(top, new Vector3(2, 2, 2))); yield return Pause();
            yield return WorldClick(top + new Vector3(.1f, 0, .1f));
            string vaseId = panel.SelectedInstanceId;
            Check(vaseId != null && panel.Document.world.instances.First(i => i.id == vaseId).location.parentId == newId, "real mouse attaches new vase to visible top surface");
            var vaseBefore = panel.Document.world.instances.First(i => i.id == vaseId).location.localPositionM.ToArray();
            var vaseClick = panel.ObjectFor(vaseId).GetComponentInChildren<Renderer>().bounds.center;
            yield return WorldClick(vaseClick);
            Check(panel.SelectedInstanceId == vaseId, "real ray selects vase separately from supporting furniture");
            yield return KeyState(Key.M); yield return KeyState();
            yield return WorldClick(top + new Vector3(.15f, 0, .1f));
            Check(panel.SelectedInstanceId == vaseId && !edit.HasPreview, "individual vase moves without selecting parent");
            var vaseAfter = panel.Document.world.instances.First(i => i.id == vaseId).location.localPositionM;
            Check(Math.Abs(Math.Abs(vaseAfter[2] - vaseBefore[2]) - .05) < .001 && Math.Abs(vaseAfter[0] - vaseBefore[0]) < .001, "actual vase move snaps exactly five centimetres in parent space");
            yield return KeyState(Key.LeftCtrl, Key.Z); yield return KeyState();
            Check(edit.RedoCount == 1 && panel.Document.world.instances.Any(i => i.id == vaseId), "Ctrl Z undoes vase move and preserves vase ID");
            Check(panel.Document.world.instances.First(i => i.id == vaseId).location.localPositionM.SequenceEqual(vaseBefore), "Undo restores exact child local offset");
            yield return Click(panel.Root.Q<Button>("parent")); Check(panel.SelectedInstanceId == newId, "real parent button selects plinth with attached vase");
            panel.cameraRig.Frame(new Bounds(new Vector3(0, .5f, 0), new Vector3(10, 2, 8))); yield return Pause();
            yield return KeyState(Key.M); yield return KeyState();
            var movePoint = panel.cameraRig.View.WorldToScreenPoint(new Vector3(-3, 0, 2)); MouseState(movePoint); yield return Pause();
            Check(panel.Editor.Preview.ObjectCount == 2 && edit.PreviewValid, "moving parent previews its child assembly");
            yield return Capture("assembly-preview");
            yield return WorldClick(new Vector3(-3, 0, 2));
            Check(panel.Document.world.instances.First(i => i.id == vaseId).location.parentId == newId, "committed parent move keeps child attachment");
            var oldVasePosition = panel.ObjectFor(vaseId).transform.position;
            yield return KeyState(Key.E); yield return KeyState();
            Check(Vector3.Distance(oldVasePosition, panel.ObjectFor(vaseId).transform.position) > .1f && panel.Document.world.instances.First(i => i.id == vaseId).location.localPositionM.SequenceEqual(vaseBefore), "rotating placed parent moves child in world while preserving attachment offset");
            yield return KeyState(Key.Delete); yield return KeyState();
            Check(panel.Document.world.instances.First(i => i.id == newId).location.kind == "storage" && edit.Inventory().storedCount >= 4, "Delete stores assembly with child");
            yield return Click(panel.Root.Q<Button>("inventory-tab"));
            var storedCard = panel.Root.Query<Button>(className: "card").ToList().First(c => c.tooltip.Contains("작은 소품"));
            yield return Click(storedCard); Check(edit.HasPreview && edit.PreviewItem.id == newId, "inventory restores most recently stored same instance");
            int owned = edit.Inventory().ownedCount;
            yield return WorldClick(new Vector3(0, 0, -2));
            Check(!edit.HasPreview && edit.Inventory().ownedCount == owned && panel.Document.world.instances.First(i => i.id == newId).variantId == "charcoal", "stored item reinstallation keeps count and color");
            Check(panel.Document.world.instances.First(i => i.id == vaseId).location.parentId == newId, "reinstallation preserves exact attached vase");
            yield return Click(panel.Root.Q<Button>("catalog-tab"));
            yield return Click(panel.Root.Query<Button>(className: "card").First());
            yield return WorldClick(new Vector3(3, 0, -2), true);
            string firstRepeat = edit.Snapshot().instances.Last().id;
            Check(edit.HasPreview, "Shift placement continues new preview");
            yield return WorldClick(new Vector3(4.5f, 0, -2));
            Check(!edit.HasPreview && edit.Snapshot().instances.Last().id != firstRepeat, "continuous placement issues distinct second ID");
            yield return Click(panel.Root.Query<Button>(className: "card").First());
            yield return WorldClick(new Vector3(.137f, 0, -3.137f), false, true);
            var freeItem = panel.Document.world.instances.First(i => i.id == panel.SelectedInstanceId);
            Check(Math.Abs(freeItem.location.positionM[0] - .137) < .003 && Math.Abs(freeItem.location.positionM[2] + 3.137) < .003, "Alt placement disables grid while keeping millimetre precision");
            var snapshot = DecorJson.WriteWorld(edit.Snapshot()); int history = edit.UndoCount;
            yield return Click(panel.Root.Query<Button>(className: "card").First()); yield return KeyState(Key.Escape); yield return KeyState();
            Check(!edit.HasPreview && history == edit.UndoCount && snapshot == DecorJson.WriteWorld(edit.Snapshot()), "Escape preview cancel leaves draft and history untouched");
            panel.cameraRig.Frame(new Bounds(panel.ObjectFor(newId).transform.position + Vector3.up, new Vector3(3, 3, 3))); yield return Pause();
            var sampledCount = edit.Inventory().ownedCount; yield return KeyState(Key.I); yield return KeyState();
            yield return WorldClick(panel.ObjectFor(newId).transform.position + new Vector3(.3f, .5f, 0));
            Check(edit.HasPreview && edit.PreviewItem.variantId == "charcoal" && edit.PreviewWorld().instances.Count(i => i.location.kind == "parent") == panel.Document.world.instances.Count(i => i.location.kind == "parent") && edit.Inventory().ownedCount == sampledCount, "eyedropper chooses definition and color without cloning children or ownership");
            panel.Editor.Cancel();
            int materialBaseline = Resources.FindObjectsOfTypeAll<Material>().Length;
            int objectsBaseline = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            for (int i = 0; i < 50; i++)
            {
                edit.BeginNew(panel.Document.catalog.items[0]); edit.SetTarget(new ItemLocation { kind = "room", roomId = "room.a", positionM = new double[] { 6, 0, 3 }, yawDeg = 0 });
                panel.Editor.Preview.Show(edit); panel.Editor.Cancel(); yield return Pause();
            }
            Check(panel.Editor.Preview.ObjectCount == 0 && Resources.FindObjectsOfTypeAll<Material>().Length == materialBaseline && FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length == objectsBaseline, "50 real preview create/cancel cycles release objects without material growth");
            Check(DecorPlacementValidator.Validate(panel.Document.catalog, edit.Snapshot()).ok, "all actual UI edits leave valid geometry");
            panel.SelectInstance(newId); panel.FrameSelection(); yield return Pause(); yield return Capture("decor-06-result");
        }
    }
}
