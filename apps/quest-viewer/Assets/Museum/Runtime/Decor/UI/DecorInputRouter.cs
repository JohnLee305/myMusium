using UnityEngine;
using UnityEngine.InputSystem;

namespace FamilyMuseum.Decor
{
    // This component exists only in DecorPrototype. No global/static input hooks.
    public sealed class DecorInputRouter : MonoBehaviour
    {
        DecorPanel panel; DecorCamera cameraRig; InputActionMap map;
        InputAction move, frame, grid, select, pan, orbit, zoom, pointer, delta;
        InputAction toolMove, left, right, store, undo, redo, cancel, free, repeat, control, eyedropper;
        bool panning, orbiting;
        internal void UseDiagnosticDevices(Keyboard keyboard, Mouse mouse)
        { map.devices = new InputDevice[] { keyboard, mouse }; }
        public void Initialize(DecorPanel value, DecorCamera rig)
        {
            panel = value; cameraRig = rig; map = new InputActionMap("DecorPrototype");
            move = map.AddAction("CameraMove", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            frame = map.AddAction("Frame", InputActionType.Button, "<Keyboard>/f");
            grid = map.AddAction("Grid", InputActionType.Button, "<Keyboard>/g");
            select = map.AddAction("Select", InputActionType.Button, "<Mouse>/leftButton");
            pan = map.AddAction("Pan", InputActionType.Button, "<Mouse>/middleButton");
            orbit = map.AddAction("Orbit", InputActionType.Button, "<Mouse>/rightButton");
            zoom = map.AddAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            pointer = map.AddAction("Pointer", InputActionType.Value, "<Mouse>/position");
            delta = map.AddAction("PointerDelta", InputActionType.Value, "<Mouse>/delta");
            toolMove = map.AddAction("MoveItem", InputActionType.Button, "<Keyboard>/m");
            eyedropper = map.AddAction("Eyedropper", InputActionType.Button, "<Keyboard>/i");
            left = map.AddAction("RotateLeft", InputActionType.Button, "<Keyboard>/q"); right = map.AddAction("RotateRight", InputActionType.Button, "<Keyboard>/e");
            store = map.AddAction("Store", InputActionType.Button, "<Keyboard>/delete");
            undo = map.AddAction("Undo", InputActionType.Button, "<Keyboard>/z"); redo = map.AddAction("Redo", InputActionType.Button, "<Keyboard>/y");
            cancel = map.AddAction("CancelPreview", InputActionType.Button, "<Keyboard>/escape");
            free = map.AddAction("FreePosition", InputActionType.Button, "<Keyboard>/alt"); repeat = map.AddAction("RepeatPlace", InputActionType.Button, "<Keyboard>/shift");
            control = map.AddAction("Control", InputActionType.Button, "<Keyboard>/ctrl"); map.Enable();
        }
        void Update()
        {
            if (panel == null || cameraRig == null) return;
            bool text = panel.TextEditing;
            if (DecorInputPolicy.KeyboardAllowed(text))
            {
                cameraRig.Move(move.ReadValue<Vector2>(), Time.unscaledDeltaTime);
                if (frame.WasPressedThisFrame()) panel.FrameSelection();
                if (grid.WasPressedThisFrame()) panel.ToggleGrid();
                if (toolMove.WasPressedThisFrame()) panel.Editor.MoveSelected();
                if (eyedropper.WasPressedThisFrame()) panel.Editor.Eyedropper();
                if (left.WasPressedThisFrame()) panel.Editor.Rotate(-1);
                if (right.WasPressedThisFrame()) panel.Editor.Rotate(1);
                if (store.WasPressedThisFrame()) panel.Editor.Store();
                if (control.IsPressed() && undo.WasPressedThisFrame()) panel.Editor.Undo();
                if (control.IsPressed() && redo.WasPressedThisFrame()) panel.Editor.Redo();
                if (cancel.WasPressedThisFrame()) panel.Editor.Cancel();
            }
            Vector2 point = pointer.ReadValue<Vector2>();
            bool allowed = DecorInputPolicy.PointerAllowed(panel.IsOverUI(point), text);
            if (pan.WasPressedThisFrame()) panning = allowed;
            if (orbit.WasPressedThisFrame()) orbiting = allowed;
            if (!pan.IsPressed() || !allowed) panning = false;
            if (!orbit.IsPressed() || !allowed) orbiting = false;
            if (panning) cameraRig.Pan(delta.ReadValue<Vector2>());
            if (orbiting) cameraRig.Orbit(delta.ReadValue<Vector2>());
            if (allowed)
            {
                cameraRig.Zoom(zoom.ReadValue<float>() / 120f, point);
                panel.Editor.UpdatePointer(point, free.IsPressed());
                if (select.WasPressedThisFrame()) panel.Editor.Click(point, repeat.IsPressed());
            }
        }
        void OnDisable() { map?.Disable(); panning = orbiting = false; }
        void OnEnable() { map?.Enable(); }
        void OnDestroy() { map?.Dispose(); }
    }
}
