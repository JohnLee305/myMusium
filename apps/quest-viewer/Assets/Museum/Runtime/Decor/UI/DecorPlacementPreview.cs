using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum.Decor
{
    // Owns a fixed pair of materials and releases all temporary geometry on cancel/disable.
    public sealed class DecorPlacementPreview : MonoBehaviour
    {
        GameObject root; Material valid, invalid;
        string signature;
        public int ObjectCount { get { return root == null ? 0 : root.GetComponentsInChildren<Renderer>().Length; } }
        public int MaterialCount { get { return (valid == null ? 0 : 1) + (invalid == null ? 0 : 1); } }
        void Awake()
        {
            valid = Make(new Color(.15f, .8f, .4f, .48f)); invalid = Make(new Color(.9f, .16f, .12f, .5f));
        }
        static Material Make(Color color)
        {
            var mat = new Material(Shader.Find("Standard")); mat.color = color;
            mat.SetFloat("_Mode", 3); mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON"); mat.renderQueue = 3000; return mat;
        }
        public void Clear()
        { if (root != null) { root.SetActive(false); Destroy(root); } root = null; signature = null; }
        public void Show(DecorInteraction interaction)
        {
            var item = interaction.PreviewItem;
            if (item == null || item.location.kind == "storage") { Clear(); return; }
            var world = interaction.PreviewWorld();
            var members = world.instances.Where(i => i.id == item.id || i.location.kind == "parent" && i.location.parentId == item.id).ToArray();
            string next = string.Join("|", members.Select(i => i.id));
            if (signature != next)
            {
                Clear(); root = new GameObject("Decor placement preview"); root.transform.SetParent(transform); signature = next;
                foreach (var member in members)
                {
                    var definition = interaction.Definition(member);
                    var shape = GameObject.CreatePrimitive(definition.kind == "surfaceDecor" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    shape.name = member.id; shape.transform.SetParent(root.transform); var collider = shape.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    shape.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            foreach (var member in members)
            {
                Frame(world, member, out var position, out var yaw);
                var definition = interaction.Definition(member); var shape = root.transform.Find(member.id);
                shape.position = position + Quaternion.Euler(0, yaw, 0) * V(definition.collision.centerM); shape.rotation = Quaternion.Euler(0, yaw, 0);
                var size = V(definition.collision.sizeM); if (definition.kind == "surfaceDecor") size.y *= .5f;
                shape.localScale = size; shape.GetComponent<Renderer>().sharedMaterial = interaction.PreviewValid ? valid : invalid;
            }
        }
        internal static Vector3 V(double[] p) { return new Vector3((float)p[0], (float)p[1], (float)p[2]); }
        internal static void Frame(DecorWorld world, ItemInstance item, out Vector3 position, out float yaw)
        {
            yaw = (float)(item.location.yawDeg ?? 0);
            if (item.location.kind == "room") { position = V(item.location.positionM); return; }
            var parent = world.instances.First(i => i.id == item.location.parentId);
            Frame(world, parent, out var pp, out var py);
            position = pp + Quaternion.Euler(0, py, 0) * V(item.location.localPositionM); yaw += py;
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { Clear(); Destroy(valid); Destroy(invalid); }
    }
}
