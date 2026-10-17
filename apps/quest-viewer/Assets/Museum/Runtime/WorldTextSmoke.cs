using System.Collections;
using System.IO;
using UnityEngine;

namespace FamilyMuseum
{
    // Opt-in GPU regression: exercise each loaded sign's actual font material.
    public sealed class WorldTextSmoke : MonoBehaviour
    {
        public bool Passed { get; private set; } = true;
        public IEnumerator Run(string folder)
        {
            var root = new GameObject("Text occlusion test");
            root.transform.position = new Vector3(0, 1000, 0);
            var camera = root.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.orthographicSize = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 10;
            camera.cullingMask = 1 << 30;
            var target = new RenderTexture(256, 128, 24);
            camera.targetTexture = target;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform, false);
            wall.layer = 30; wall.transform.localPosition = Vector3.forward * 2;
            wall.transform.localScale = new Vector3(8, 4, .2f);
            var material = new Material(Shader.Find("Standard")) { color = Color.black };
            wall.GetComponent<Renderer>().sharedMaterial = material;
            try
            {
                foreach (string name in new[] { "Museum title", "Museum subtitle" })
                {
                    var source = GameObject.Find(name).GetComponent<TextMesh>();
                    var label = Instantiate(source, root.transform);
                    label.gameObject.layer = 30;
                    label.transform.localPosition = Vector3.forward * 4;
                    label.transform.localRotation = Quaternion.identity;
                    label.transform.localScale = Vector3.one;
                    label.text = "WALL TEXT 0123"; label.color = Color.white;
                    label.fontSize = 64; label.characterSize = .04f;
                    label.anchor = TextAnchor.MiddleCenter;
                    label.font.RequestCharactersInTexture(label.text, 64, label.fontStyle);
                    var renderer = label.GetComponent<Renderer>();
                    yield return null; // Let the dynamic font atlas update before GPU readback.
                    wall.SetActive(false); renderer.enabled = true;
                    var visible = Capture(camera, target, Path.Combine(folder, name + "-visible.png"));
                    renderer.enabled = false;
                    int visiblePixels = Difference(visible, Capture(camera, target, null));
                    wall.SetActive(true); renderer.enabled = true;
                    var blocked = Capture(camera, target, Path.Combine(folder, name + "-blocked.png"));
                    renderer.enabled = false;
                    int leakingPixels = Difference(blocked, Capture(camera, target, null));
                    bool ok = visiblePixels > 20 && leakingPixels == 0;
                    Passed &= ok;
                    Debug.Log($"WORLD_TEXT_OCCLUSION: label={name} visiblePixels={visiblePixels} leakingPixels={leakingPixels} passed={ok}");
                    label.gameObject.SetActive(false); Destroy(label.gameObject);
                }
            }
            finally
            {
                camera.targetTexture = null;
                target.Release(); Destroy(target); Destroy(material); Destroy(root);
            }
            Debug.Log(Passed ? "WORLD_TEXT_SMOKE_PASS" : "WORLD_TEXT_SMOKE_FAIL");
        }
        static Color32[] Capture(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                if (path != null) File.WriteAllBytes(path, texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; Destroy(texture); }
        }
        static int Difference(Color32[] a, Color32[] b)
        {
            int pixels = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 12) pixels++;
            return pixels;
        }
    }
}
