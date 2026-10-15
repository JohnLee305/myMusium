using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyMuseum
{
    // A separate render-only snapshot; never rotates or scales the live exhibit.
    public sealed class ArtifactInspection : MonoBehaviour
    {
        public bool Active => pivot != null && target != null;
        public float Zoom { get; private set; } = 1;
        public Vector2 Angles { get; private set; }
        public RenderTexture Texture => target;
        GameObject stage, pivot;
        Camera view;
        RenderTexture target;
        readonly List<Mesh> baked = new List<Mesh>();
        bool dragging;
        const int Layer = 31;

        public bool Open(ArtifactDisplay slot)
        {
            Close();
            if (slot == null || !slot.Loaded) return false;
            try
            {
                stage = new GameObject("Inspection stage");
                pivot = new GameObject("Inspection pivot"); pivot.transform.SetParent(stage.transform, false);
                var renderers = slot.modelSlot.GetComponentsInChildren<Renderer>();
                bool any = false; Bounds bounds = new Bounds();
                foreach (var original in renderers)
                {
                    if (!original.enabled || !original.gameObject.activeInHierarchy) continue;
                    Mesh mesh;
                    if (original is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh); baked.Add(mesh); }
                    else { mesh = original.GetComponent<MeshFilter>()?.sharedMesh; }
                    if (mesh == null) continue;
                    var part = new GameObject("Inspection mesh"); part.layer = Layer;
                    part.transform.SetParent(pivot.transform, false);
                    part.transform.localPosition = original.transform.position - slot.modelSlot.position;
                    part.transform.localRotation = original.transform.rotation;
                    part.transform.localScale = original.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var copy = part.AddComponent<MeshRenderer>(); copy.sharedMaterials = original.sharedMaterials;
                    copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = false;
                    copy.lightProbeUsage = LightProbeUsage.Off; copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    if (!any) { bounds = copy.bounds; any = true; } else bounds.Encapsulate(copy.bounds);
                }
                if (!any || bounds.size.magnitude < .00001f) { Close(); return false; }
                Vector3 center = bounds.center - stage.transform.position;
                foreach (Transform child in pivot.transform) child.localPosition -= center;
                pivot.transform.localScale = Vector3.one * (2f / bounds.size.magnitude);
                stage.transform.position = new Vector3(10000, 10000, 10000);
                var cameraObject = new GameObject("Inspection camera"); cameraObject.transform.SetParent(stage.transform, false);
                view = cameraObject.AddComponent<Camera>(); view.enabled = false;
                view.cullingMask = 1 << Layer; view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = new Color(.12f,.16f,.17f); view.fieldOfView = 35; view.nearClipPlane = .01f; view.farClipPlane = 20;
                view.allowHDR = false; view.allowMSAA = false;
                target = new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32); target.Create(); view.targetTexture = target;
                foreach (float yaw in new[] { -35f, 145f })
                {
                    var lamp = new GameObject("Inspection light"); lamp.transform.SetParent(stage.transform, false);
                    lamp.transform.localRotation = Quaternion.Euler(35,yaw,0);
                    var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.cullingMask = 1 << Layer;
                    light.intensity = yaw < 0 ? 1.2f : .65f; light.shadows = LightShadows.None;
                }
                ResetView(); return true;
            }
            catch (System.Exception error) { Debug.LogWarning("INSPECTION_FAILED: " + error.Message); Close(); return false; }
        }
        public void Rotate(Vector2 delta) { Angles += delta; Angles = new Vector2(Mathf.Repeat(Angles.x,360),Mathf.Clamp(Angles.y,-85,85)); Render(); }
        public void SetZoom(float value) { Zoom = Mathf.Clamp(value,.6f,2.5f); Render(); }
        public void ResetView() { Angles = new Vector2(25,-15); Zoom = 1; dragging = false; Render(); }
        void Render()
        {
            if (!Active) return;
            pivot.transform.localRotation = Quaternion.Euler(Angles.y,Angles.x,0);
            view.transform.localPosition = new Vector3(0,0,-3.7f / Zoom); view.transform.localRotation = Quaternion.identity;
            view.Render();
        }
        public void Draw(Rect area)
        {
            if (!Active) return;
            GUI.DrawTexture(area,target,ScaleMode.ScaleToFit,false);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition)) { dragging = true; e.Use(); }
            else if (e.type == EventType.MouseUp && e.button == 0) dragging = false;
            else if (e.type == EventType.MouseDrag && dragging) { Rotate(new Vector2(e.delta.x,-e.delta.y)*.5f); e.Use(); }
            else if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition)) { SetZoom(Zoom * Mathf.Exp(-e.delta.y*.08f)); e.Use(); }
        }
        public void Close()
        {
            dragging = false;
            if (view != null) view.targetTexture = null;
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            foreach (var mesh in baked) Destroy(mesh); baked.Clear();
            if (target != null) { target.Release(); Destroy(target); }
            target = null; pivot = null; stage = null; view = null;
        }
        void OnDestroy() { Close(); }
    }
}
