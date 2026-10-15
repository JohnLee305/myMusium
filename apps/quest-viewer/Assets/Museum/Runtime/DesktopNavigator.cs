using UnityEngine;
using UnityEngine.InputSystem;

namespace FamilyMuseum
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopNavigator : MonoBehaviour
    {
        public Transform view;
        [SerializeField, Min(1f)] float keyboardLookSpeed = 65f;
        float pitch;
        float verticalVelocity;
        ArtifactDetailsPanel details;
        void Start() { details = FindFirstObjectByType<ArtifactDetailsPanel>(); }

        void Update()
        {
            if (details == null) details = FindFirstObjectByType<ArtifactDetailsPanel>();
            if (details != null && details.BlocksNavigation) return;
            var keys = Keyboard.current;
            if (keys == null) return;
            bool look = Mouse.current != null && Mouse.current.rightButton.isPressed && !keys.escapeKey.isPressed;
            Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !look;
            float yaw = ((keys.eKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0)
                - (keys.qKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0)) * keyboardLookSpeed * Time.deltaTime;
            float pitchDelta = ((keys.downArrowKey.isPressed ? 1 : 0)
                - (keys.upArrowKey.isPressed ? 1 : 0)) * keyboardLookSpeed * Time.deltaTime;
            if (look)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * 0.12f;
                yaw += delta.x;
                pitchDelta -= delta.y;
            }
            transform.Rotate(0f, yaw, 0f);
            pitch = Mathf.Clamp(pitch + pitchDelta, -75f, 75f);
            view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Vector2 direction = new Vector2(
                (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            direction = Vector2.ClampMagnitude(direction, 1f);
            var controller = GetComponent<CharacterController>();
            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + Physics.gravity.y * Time.deltaTime;
            Vector3 velocity = (transform.forward * direction.y + transform.right * direction.x) * 2f;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

