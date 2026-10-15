using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyMuseum
{
    // Only mesh data is copied. Imported scripts, physics and importers are never duplicated.
    public sealed class ExhibitReplica : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        public Bounds SourceBounds { get; private set; }
        public static ExhibitReplica Create(ArtifactDisplay source, Material outline = null)
        {
            if (source == null || !source.Loaded) return null;
            var root = new GameObject(outline == null ? "Held exhibit replica" : "Exhibit focus outline");
            var replica = root.AddComponent<ExhibitReplica>();
            bool any = false; Bounds bounds = default;
            try
            {
                foreach (var renderer in source.modelSlot.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Mesh mesh = null;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        mesh = new Mesh(); skin.BakeMesh(mesh); replica.ownedMeshes.Add(mesh);
                    }
                    else mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    var part = new GameObject("Replica mesh"); part.transform.SetParent(root.transform, false);
                    part.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);
                    part.transform.localScale = renderer.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var copy = part.AddComponent<MeshRenderer>();
                    if (outline == null) copy.sharedMaterials = renderer.sharedMaterials;
                    else
                    {
                        var mats = new Material[mesh.subMeshCount];
                        for (int i=0;i<mats.Length;i++) mats[i]=outline;
                        copy.sharedMaterials=mats;
                    }
                    copy.shadowCastingMode=ShadowCastingMode.Off; copy.receiveShadows=false;
                    copy.lightProbeUsage=LightProbeUsage.Off; copy.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    if (!any) { bounds=copy.bounds; any=true; } else bounds.Encapsulate(copy.bounds);
                }
                if (!any || bounds.size.magnitude<.000001f) { Destroy(root); return null; }
                foreach (Transform part in root.transform) part.position-=bounds.center;
                root.transform.position=bounds.center; replica.SourceBounds=bounds;
                return replica;
            }
            catch { root.SetActive(false); Destroy(root); throw; }
        }
        public void Release() { gameObject.SetActive(false); Destroy(gameObject); }
        void OnDestroy() { foreach(var mesh in ownedMeshes) Destroy(mesh); }
    }
}
