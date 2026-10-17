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
            panel.SelectInstance("plinth.001"); panel.FrameSelection(); yield return Pause();
            yield return Capture("selected-item");
            File.WriteAllLines(Path.Combine(output, "results.txt"), results.Concat(new[] { (failed ? "DECOR_UI_FAIL" : "DECOR_UI_PASS") + " checks=" + checks }));
            Debug.Log((failed ? "DECOR_UI_FAIL" : "DECOR_UI_PASS") + " checks=" + checks);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); yield return new WaitForSecondsRealtime(.3f); Application.Quit(failed ? 1 : 0);
        }
    }
}
