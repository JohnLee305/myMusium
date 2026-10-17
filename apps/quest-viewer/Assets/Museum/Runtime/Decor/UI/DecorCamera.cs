using UnityEngine;

namespace FamilyMuseum.Decor
{
    public sealed class DecorCamera : MonoBehaviour
    {
        public Camera View { get; private set; }
        public Vector3 Pivot { get; private set; }
        public float Distance { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        EditorPresentationSettings settings;
        public void Initialize(Camera view, EditorPresentationSettings value)
        {
            View = view; settings = value; Pivot = new Vector3(0, .6f, 0);
            Distance = settings.defaultDistance; Pitch = settings.initialPitch; Yaw = settings.initialYaw; Refresh();
        }
        public void Move(Vector2 direction, float deltaTime)
        {
            var turn = Quaternion.Euler(0, Yaw, 0);
            Pivot += turn * new Vector3(direction.x, 0, direction.y) * Distance * settings.panSpeed * deltaTime;
            Pivot = new Vector3(Mathf.Clamp(Pivot.x, -14, 14), Mathf.Clamp(Pivot.y, 0, 3), Mathf.Clamp(Pivot.z, -12, 12)); Refresh();
        }
        public void Pan(Vector2 pixels)
        {
            var right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
            var forward = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            Pivot += (-right * pixels.x - forward * pixels.y) * Distance / Mathf.Max(200, View.pixelHeight);
            Move(Vector2.zero, 0);
        }
        public void Orbit(Vector2 pixels)
        { Yaw = Mathf.Repeat(Yaw + pixels.x * settings.orbitSpeed, 360); Pitch = Mathf.Clamp(Pitch - pixels.y * settings.orbitSpeed, settings.minPitch, settings.maxPitch); Refresh(); }
        public void Zoom(float wheel, Vector2 screen)
        {
            if (Mathf.Approximately(wheel, 0)) return;
            var floor = new Plane(Vector3.up, Pivot);
            var before = View.ScreenPointToRay(screen); Vector3 anchor = Pivot;
            if (floor.Raycast(before, out float d)) anchor = before.GetPoint(d);
            Distance = Mathf.Clamp(Distance * Mathf.Exp(-Mathf.Clamp(wheel, -4, 4) * settings.zoomSpeed), settings.minDistance, settings.maxDistance); Refresh();
            var after = View.ScreenPointToRay(screen);
            if (floor.Raycast(after, out d)) { Pivot += anchor - after.GetPoint(d); Move(Vector2.zero, 0); }
        }
        public void Frame(Bounds bounds)
        {
            Pivot = bounds.center;
            float vertical = View.fieldOfView * Mathf.Deg2Rad * .5f;
            float horizontal = Mathf.Atan(Mathf.Tan(vertical) * View.aspect);
            Distance = Mathf.Clamp(bounds.extents.magnitude / Mathf.Sin(Mathf.Min(vertical, horizontal)) * 1.25f, settings.minDistance, settings.maxDistance);
            Refresh();
        }
        void Refresh() { transform.rotation = Quaternion.Euler(Pitch, Yaw, 0); transform.position = Pivot - transform.forward * Distance; }
    }
}
