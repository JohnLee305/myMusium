using UnityEngine;
using UnityEngine.InputSystem;

namespace FamilyMuseum.Decor
{
    // This component exists only in DecorPrototype. No global/static input hooks.
    public sealed class DecorInputRouter : MonoBehaviour
    {
        DecorPanel panel; DecorCamera cameraRig; InputActionMap map;
        InputAction move, frame, grid, select, pan, orbit, zoom, pointer, delta;
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
            delta = map.AddAction("PointerDelta", InputActionType.Value, "<Mouse>/delta"); map.Enable();
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
                if (select.WasPressedThisFrame()) panel.SelectAt(point);
            }
        }
        void OnDisable() { map?.Disable(); panning = orbiting = false; }
        void OnEnable() { map?.Enable(); }
        void OnDestroy() { map?.Dispose(); }
    }
}
