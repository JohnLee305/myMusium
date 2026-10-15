using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FamilyMuseum.Editor
{
    public static class VisitorAssetSetup
    {
        const string ModelPath="Assets/Museum/Visitors/Source/Casual_2.fbx";
        const string PrefabPath="Assets/Museum/Visitors/Casual2.prefab";
        const string ProfilePath="Assets/Museum/Visitors/Casual2.asset";
        const string CatalogPath="Assets/Museum/Resources/VisitorCatalog.asset";
        [Serializable] sealed class CatalogExport {public string schemaVersion="1.0";public AppearanceExport[] appearances;}
        [Serializable] sealed class AppearanceExport
        {
            public string id,displayName,creator,license;
            public bool available;
            public float weight;
        }
        public static void ExportCatalog(string root)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<VisitorCatalog>(CatalogPath);
            if(catalog==null) throw new Exception("Visitor catalog missing");
            var entries=catalog.appearances.Where(p=>p!=null && p.id!=null && System.Text.RegularExpressions.Regex.IsMatch(p.id,@"^[a-z0-9][a-z0-9-]{0,63}$")).GroupBy(p=>p.id).Select(group=>group.First()).Take(256).Select(p=>new AppearanceExport {
                id=p.id,displayName=string.IsNullOrWhiteSpace(p.displayName)?p.id:p.displayName.Substring(0,Math.Min(80,p.displayName.Length)),
                creator=p.creator,license=p.license,available=p.IsUsable && p.weight<=100,
                weight=float.IsNaN(p.weight) || float.IsInfinity(p.weight)?0:Mathf.Clamp(p.weight,0,100)
            }).ToArray();
            var json=JsonUtility.ToJson(new CatalogExport{appearances=entries},true)+"\n";
            File.WriteAllText(Path.Combine(root,"apps/curator-admin/visitor-catalog.json"),json);
            File.WriteAllText(Path.Combine(root,"Builds/Windows/visitor-catalog.json"),json);
            Debug.Log("VISITOR_ADMIN_CATALOG_EXPORTED: "+entries.Length);
        }
        [MenuItem("Museum/Visitors/Prepare initial appearance")]
        public static void Prepare()
        {
            if(!File.Exists(ModelPath))
            {
                if(AssetDatabase.LoadAssetAtPath<VisitorCatalog>(CatalogPath)!=null) return;
                throw new Exception("Visitor source missing: "+ModelPath);
            }
            var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if(importer.animationType!=ModelImporterAnimationType.Generic || !importer.importAnimation || importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.animationType=ModelImporterAnimationType.Generic;
                importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation=true; importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            var clips=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            AnimationClip Find(string name)=>clips.FirstOrDefault(c=>c.name==name || c.name.EndsWith("|"+name));
            var idle=Find("Idle_Neutral"); var walk=Find("Walk");
            if(idle==null || walk==null || idle.length<=0 || walk.length<=0) throw new Exception("Required visitor clips missing");
            // Loop only the locomotion clips; other assets and additional appearances are not rewritten.
            var definitions=importer.clipAnimations;
            if(definitions.Length==0) definitions=importer.defaultClipAnimations;
            bool changed=false;
            foreach(var definition in definitions)
                if((definition.name==idle.name || definition.name==walk.name) && !definition.loopTime)
                { definition.loopTime=true; definition.lockRootPositionXZ=true; definition.lockRootRotation=true; changed=true; }
            if(changed)
            {
                importer.clipAnimations=definitions; importer.SaveAndReimport();
                clips=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
                idle=Find("Idle_Neutral"); walk=Find("Walk");
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                try
                {
                    instance.name="Casual2 visual";
                    foreach(var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
                    foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled=true;
                    if(instance.GetComponentInChildren<Animator>(true)==null) instance.AddComponent<Animator>();
                    prefab=PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            var profile=AssetDatabase.LoadAssetAtPath<VisitorAppearance>(ProfilePath);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VisitorAppearance>();
                profile.id="quaternius-casual2"; profile.displayName="일상복 관람객";
                profile.prefab=prefab; profile.idle=idle; profile.walk=walk;
                profile.height=1.75f; profile.walkSpeed=.9f;
                profile.creator="Quaternius"; profile.sourceUrl="https://quaternius.com/packs/ultimatemodularcharacters.html";
                profile.license="CC0 1.0 (included pack License.txt)";
                AssetDatabase.CreateAsset(profile,ProfilePath);
            }
            var catalog=AssetDatabase.LoadAssetAtPath<VisitorCatalog>(CatalogPath);
            if(catalog==null)
            {
                catalog=ScriptableObject.CreateInstance<VisitorCatalog>(); catalog.appearances.Add(profile);
                AssetDatabase.CreateAsset(catalog,CatalogPath);
            }
            // Preserve additions, removals, weights, disable flags and tuning across builds.
            AssetDatabase.SaveAssets();
            Debug.Log($"VISITOR_ASSETS_READY: entries={catalog.GetAvailable().Count} clips={clips.Length} idle={idle.name} walk={walk.name}");
        }
    }
}
