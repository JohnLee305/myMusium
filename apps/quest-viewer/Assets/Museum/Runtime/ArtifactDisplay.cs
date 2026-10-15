using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed class ArtifactDisplay : MonoBehaviour
    {
        public Transform modelSlot;
        public GameObject placeholder;
        public TextMesh titleLabel;
        public TextMesh descriptionLabel;
        public bool IsFinished { get; private set; }
        public bool Loaded { get; private set; }
        public ArtifactData Data { get; private set; }
        public RoomTheme Theme = RoomTheme.Default;
        public string StateLabel { get; private set; } = "Loading artifact...";
        GltfImport importer;
        GameObject instance;
        Font labelFont;
        GameObject plaque;
        Material plaqueFace, plaqueFrame;
        public static int ActiveImports { get; private set; }

        public bool managedSlot;
        async void Start()
        {
            if (!managedSlot) await gameObject.AddComponent<GalleryDisplay>().LoadAsync(this);
        }

        public async Task LoadAsync(LocalArchive archive, string relative, string manifestError = null)
        {
            var token = destroyCancellationToken;
            placeholder.SetActive(false);
            try
            {
                labelFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 64);
                labelFont.material.shader = Resources.Load<Shader>("WorldText");
                BuildPlaque();
                if (manifestError != null) throw new InvalidDataException(manifestError);
                ArtifactData data = archive.ReadArtifact(relative);
                Data = data;
                byte[] bytes = archive.ReadGlb(data.model);
                var logger = new CollectingLogger();
                importer = new GltfImport(logger: logger);
                ActiveImports++;
                if (!await importer.Load(bytes, cancellationToken: token)) throw new InvalidDataException("GLB_LOAD_FAILED");
                token.ThrowIfCancellationRequested();
                instance = new GameObject(data.id);
                instance.transform.SetParent(modelSlot, false);
                if (!await importer.InstantiateMainSceneAsync(instance.transform, cancellationToken: token)) throw new InvalidDataException("GLB_INSTANTIATION_FAILED");
                token.ThrowIfCancellationRequested();
                if (logger.Items != null && logger.Items.Any(item => item.Type == LogType.Error)) throw new InvalidDataException("GLB_PARTIAL_LOAD_ERROR");
                instance.transform.localScale = Vector3.one * data.scale;
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidDataException("GLB_HAS_NO_MESH");
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported) throw new InvalidDataException("GLB_SHADER_UNSUPPORTED");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                // Keep models inside the plinth footprint and the visitor aisle; never silently rescale.
                if (bounds.size.x > 0.95f || bounds.size.z > 0.95f || bounds.size.y > 1.8f) throw new InvalidDataException("MODEL_EXCEEDS_SLOT: reduce artifact scale");
                instance.transform.position += new Vector3(modelSlot.position.x - bounds.center.x,
                    modelSlot.position.y - bounds.min.y, modelSlot.position.z - bounds.center.z);
                // A trigger volume makes even thin imported meshes selectable without blocking movement.
                var selection = modelSlot.gameObject.AddComponent<BoxCollider>();
                selection.isTrigger = true;
                selection.center = modelSlot.InverseTransformPoint(new Vector3(modelSlot.position.x, modelSlot.position.y + bounds.size.y / 2f, modelSlot.position.z));
                selection.size = new Vector3(Mathf.Max(.15f, bounds.size.x), Mathf.Max(.15f, bounds.size.y), Mathf.Max(.15f, bounds.size.z));
                SetLabel(titleLabel, Preview(data.title, 16, 1), 0.80f, 0.14f);
                SetLabel(descriptionLabel, Preview(data.description, 14, 2), 0.80f, 0.16f);
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                Fit(titleLabel, 0.80f, 0.14f); Fit(descriptionLabel, 0.80f, 0.16f);
                Loaded = true;
                StateLabel = "Artifact loaded from archive";
                Debug.Log($"ARTIFACT_LOADED: {data.id} | {data.title} | {data.model} | meshes={renderers.Length}");
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested) return;
                if (instance != null) { instance.SetActive(false); Destroy(instance); }
                placeholder.SetActive(true);
                SetLabel(titleLabel, "전시품 확인 필요", 0.80f, 0.14f);
                SetLabel(descriptionLabel, "자료를 불러오지 못했습니다.\n선택하면 안내를 볼 수 있습니다.", 0.80f, 0.16f);
                StateLabel = "Artifact unavailable - check archive files";
                Debug.LogWarning("ARTIFACT_FAILED: " + relative + " | " + exception.Message);
            }
            finally
            {
                if (!token.IsCancellationRequested) IsFinished = true;
                if (!Loaded) ReleaseImporter();
            }
        }
        void BuildPlaque()
        {
            plaque = new GameObject("Exhibit information plaque");
            plaque.transform.SetParent(titleLabel.transform.parent, false);
            plaqueFace = new Material(Shader.Find("Standard")) { color = Theme.Color("plaqueBackground") };
            plaqueFrame = new Material(Shader.Find("Standard")) { color = Theme.Color("plaqueBorder") };
            plaqueFrame.SetFloat("_Metallic", .5f);
            plaqueFrame.SetFloat("_Glossiness", .3f);
            PlaqueBlock("Brass frame", new Vector3(0, .65f, .276f), new Vector3(.96f, .64f, .032f), plaqueFrame);
            PlaqueBlock("Green face", new Vector3(0, .65f, .252f), new Vector3(.925f, .605f, .016f), plaqueFace);
            PlaqueBlock("Fine divider", new Vector3(0, .716f, .239f), new Vector3(.80f, .004f, .004f), plaqueFrame);
            foreach (var label in new[] { titleLabel, descriptionLabel })
            {
                label.anchor = TextAnchor.UpperLeft;
                label.alignment = TextAlignment.Left;
                label.color = Theme.Color("plaqueText");
                label.richText = false;
                label.transform.localPosition = new Vector3(-.40f, label == titleLabel ? .85f : .677f, .233f);
            }
            var hint = new GameObject("Read more hint").AddComponent<TextMesh>();
            hint.transform.SetParent(plaque.transform, false);
            hint.transform.localPosition = new Vector3(-.40f, .423f, .233f);
            hint.anchor = TextAnchor.UpperLeft;
            hint.alignment = TextAlignment.Left;
            hint.color = Theme.Color("plaqueHint");
            SetLabel(hint, "선택하여 이야기 읽기", .80f, .05f);
        }
        void PlaqueBlock(string name, Vector3 position, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(plaque.transform, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            // Keep the plaque clickable as part of the exhibit; no additional movement barrier.
            block.GetComponent<Collider>().isTrigger = true;
        }
        static string Preview(string text, int columns, int lines)
        {
            string singleLine = string.Join(" ", text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            int limit = columns * lines;
            if (singleLine.Length > limit) singleLine = singleLine.Substring(0, limit - 1) + "…";
            return Wrap(singleLine, columns);
        }
        void SetLabel(TextMesh label, string text, float width, float height)
        {
            label.transform.localScale = Vector3.one;
            if (labelFont != null)
            {
                label.font = labelFont;
                label.fontSize = 64;
                labelFont.RequestCharactersInTexture(text, 64);
                label.GetComponent<Renderer>().sharedMaterial = labelFont.material;
            }
            label.characterSize = 0.014f;
            label.text = text;
            Fit(label, width, height);
        }
        static void Fit(TextMesh label, float width, float height)
        {
            Bounds bounds = label.GetComponent<Renderer>().bounds;
            float factor = Mathf.Min(1f, width / Mathf.Max(bounds.size.x, 0.001f), height / Mathf.Max(bounds.size.y, 0.001f));
            label.transform.localScale *= factor;
        }
        static string Wrap(string text, int columns)
        {
            var output = new StringBuilder();
            int column = 0;
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n') { output.Append(c); column = 0; continue; }
                if (column >= columns) { output.Append('\n'); column = 0; }
                output.Append(c); column++;
            }
            return output.ToString();
        }
        void OnDestroy()
        {
            if (Loaded) ReleaseImporter();
            if (labelFont != null) Destroy(labelFont);
            if (plaque != null) Destroy(plaque);
            if (plaqueFace != null) Destroy(plaqueFace);
            if (plaqueFrame != null) Destroy(plaqueFrame);
        }
        void ReleaseImporter()
        {
            if (importer == null) return;
            importer.Dispose(); importer = null; ActiveImports--;
        }
    }
}



