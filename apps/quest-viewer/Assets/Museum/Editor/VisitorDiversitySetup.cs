using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FamilyMuseum.Editor
{
    public static class VisitorDiversitySetup
    {
        const string Folder="Assets/Museum/Visitors/Diversity";
        public static void GenerateAndBuild(){Generate();MuseumSetup.BuildDesktop();}
        [Serializable] sealed class Recipe { public string id,source; public string[] parts; public string signature; public int palette; }
        [Serializable] sealed class Recipes { public string schemaVersion="1.0"; public Recipe[] appearances; }
        public static string MeshSignature(SkinnedMeshRenderer renderer)
        {
            using var stream=new MemoryStream(); using var writer=new BinaryWriter(stream);
            foreach(var v in renderer.sharedMesh.vertices) {writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
            foreach(var i in renderer.sharedMesh.triangles) writer.Write(i);
            foreach(var m in renderer.sharedMaterials) {var c=m!=null && m.HasProperty("_Color")?m.color:Color.white;writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);}
            writer.Flush(); using var hash=SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","");
        }
        [MenuItem("Museum/Visitors/Generate 100 modular appearances")]
        public static void Generate()
        {
            VisitorAssetSetup.Prepare();
            Directory.CreateDirectory(Folder+"/Palettes"); AssetDatabase.Refresh();
            var catalog=AssetDatabase.LoadAssetAtPath<VisitorCatalog>("Assets/Museum/Resources/VisitorCatalog.asset");
            var recipes=new List<Recipe>();
            GenerateFamily("men","Men_Master",new[]{"Casual2_Head","Casual_Head","Beach_Head","Punk_Head"},new[]{"Casual2_Body","Casual_Body","Suit_Body"},new[]{"Casual2_Legs","Casual_Legs","Suit_Legs"},new[]{"Casual2_Feet","Suit_Feet"},catalog,recipes);
            GenerateFamily("women","Women_Master",new[]{"Casual_Head","Formad_Head","Suit_Head","Punk_Head"},new[]{"Casual_Body","Formal_Body","Suit_Body"},new[]{"Casual_Legs","Formal_Legs","Suit_Legs"},new[]{"Casual_Feet","Suit_Feet"},catalog,recipes);
            if(recipes.Count!=100 || recipes.Select(r=>r.signature).Distinct().Count()!=100) throw new Exception("100 distinct visual recipes required");
            File.WriteAllText(Path.Combine(Folder,"recipes.json"),JsonUtility.ToJson(new Recipes{appearances=recipes.ToArray()},true)+"\n");
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("VISITOR_DIVERSITY_GENERATED: recipes=100 available="+catalog.GetAvailable().Count);
        }
        static void GenerateFamily(string family,string model,string[] heads,string[] bodies,string[] legs,string[] feet,VisitorCatalog catalog,List<Recipe> recipes)
        {
            string source="Assets/Museum/Visitors/Source/"+model+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(source);
            if(importer==null) throw new Exception("Missing source "+source);
            importer.animationType=ModelImporterAnimationType.Generic; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true; importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            var definitions=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
            foreach(var clip in definitions) if(clip.name.EndsWith("Walk") || clip.name.EndsWith("Idle_Neutral"))
            {clip.loopTime=true;clip.lockRootPositionXZ=true;clip.lockRootRotation=true;}
            importer.clipAnimations=definitions; importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            var walk=clips.First(c=>c.name.EndsWith("Walk"));var idle=clips.First(c=>c.name.EndsWith("Idle_Neutral"));
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            var renderers=asset.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>r.name);
            var hashes=renderers.ToDictionary(pair=>pair.Key,pair=>MeshSignature(pair.Value));
            var options=new List<string[]>();var unique=new HashSet<string>();
            foreach(var h in heads) foreach(var b in bodies) foreach(var l in legs) foreach(var f in feet)
            {var parts=new[]{h,b,l,f};if(parts.Any(p=>!renderers.ContainsKey(p)))throw new Exception("Part missing");if(unique.Add(string.Join("|",parts.Select(p=>hashes[p])))) options.Add(parts);}
            var random=new System.Random(family=="men"?21491:85129);
            for(int i=options.Count-1;i>0;i--) {int n=random.Next(i+1);var tmp=options[i];options[i]=options[n];options[n]=tmp;}
            if(options.Count<50) throw new Exception("Not enough distinct combinations: "+family+" "+options.Count);
            for(int index=0;index<50;index++)
            {
                string id=$"quaternius-{family}-{index+1:000}";
                string prefabPath=$"{Folder}/{id}.prefab",profilePath=$"{Folder}/{id}.asset";
                var parts=options[index];var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if(prefab==null)
                {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
                    try
                    {
                        PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                        foreach(var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
                        foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                            if(parts.Contains(renderer.name)) renderer.enabled=true; else UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                        if(instance.GetComponentsInChildren<SkinnedMeshRenderer>().Length!=4) throw new Exception("Exactly four parts required");
                        ApplyPalette(instance,index,asset);
                        prefab=PrefabUtility.SaveAsPrefabAsset(instance,prefabPath);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(instance);}
                }
                else
                {
                    var contents=PrefabUtility.LoadPrefabContents(prefabPath);
                    try{ApplyPalette(contents,index,asset);prefab=PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);}
                    finally{PrefabUtility.UnloadPrefabContents(contents);}
                }
                var profile=AssetDatabase.LoadAssetAtPath<VisitorAppearance>(profilePath);
                if(profile==null)
                {
                    profile=ScriptableObject.CreateInstance<VisitorAppearance>();profile.id=id;
                    profile.displayName=(family=="men"?"남성":"여성")+$" 관람객 {index+1:00}";
                    profile.prefab=prefab;profile.walk=walk;profile.idle=idle;
                    profile.height=family=="men"?1.68f+(index%5)*.035f:1.58f+(index%5)*.035f;
                    profile.creator="Quaternius";profile.license="CC0 1.0 (included pack License.txt)";
                    profile.sourceUrl="https://quaternius.com/packs/"+(family=="men"?"ultimatemodularcharacters.html":"ultimatemodularwomen.html");
                    AssetDatabase.CreateAsset(profile,profilePath);
                }
                // Editor-only preview clips are not serialized into a Player. Always bind the imported runtime clips.
                profile.walk=walk;profile.idle=idle;EditorUtility.SetDirty(profile);
                if(!catalog.appearances.Any(p=>p!=null && p.id==id)) catalog.appearances.Add(profile);
                recipes.Add(new Recipe{id=id,source=model,parts=parts,palette=index,signature=family+"|"+string.Join("|",parts.Select(p=>hashes[p]))});
            }
        }
        static readonly string[] Skin={"E8B997","C88E68","A46A45","784A32","F1D1B7"};
        static readonly string[] Hair={"292323","65432C","C89747","A64F31","B8B6B0"};
        static readonly string[] Tops={"305C79","A64D50","E1C48D","536D4B","725C8E","C9783D","3B7773","D8D8D0"};
        static readonly string[] Pants={"333B4C","684D3E","71899A","B4A58A","50584A","72566B"};
        static readonly string[] Shoes={"2B2B30","624B3B","394B65","C1B5A0"};
        static Material PaletteMaterial(Material original,string kind,string hex)
        {
            string path=$"{Folder}/Palettes/{kind}-{hex}.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(original);material.name=kind+" "+hex;
            ColorUtility.TryParseHtmlString("#"+hex,out var color);material.color=color;
            AssetDatabase.CreateAsset(material,path);return material;
        }
        static void ApplyPalette(GameObject root,int index,GameObject source)
        {
            var sources=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>r.name);
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int slot=0;slot<materials.Length;slot++)
                {
                    var original=materials[slot];string name=original.name,kind=null,hex=null;
                    // Re-running generation starts from the original imported material slots.
                    var native=sources[renderer.name].sharedMaterials[slot];
                    original=native;name=native.name;
                    if(name=="Skin" || name=="Skin_Darker"){kind="Skin";hex=Skin[index%Skin.Length];}
                    else if(renderer.name.EndsWith("_Head") && (name.Contains("Hair") || name=="Eyebrows" || name=="Moustache" || renderer.name=="Punk_Head" && name.StartsWith("Red"))){kind="Hair";hex=Hair[(index/3)%Hair.Length];}
                    else if(renderer.name.EndsWith("_Body") && name!="White" && name!="Gold"){kind="Top";hex=Tops[index%Tops.Length];}
                    else if(renderer.name.EndsWith("_Legs") && name!="Gold"){kind="Pants";hex=Pants[(index/2)%Pants.Length];}
                    else if(renderer.name.EndsWith("_Feet") && name!="White"){kind="Shoes";hex=Shoes[(index/4)%Shoes.Length];}
                    materials[slot]=kind==null?original:PaletteMaterial(original,kind,hex);
                }
                renderer.sharedMaterials=materials;
            }
        }
    }
}
