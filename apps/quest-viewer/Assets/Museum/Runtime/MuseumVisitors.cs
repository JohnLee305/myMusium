using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumVisitors : MonoBehaviour
    {
        public VisitorPaths Paths { get; private set; }
        public int ActiveCount=>guests.Count;
        public int Entered { get; private set; }
        public int Exited { get; private set; }
        public int Views { get; private set; }
        public int Aborted { get; private set; }
        public int TargetCount { get; private set; }=1;
        public bool SmokePassed { get; private set; }
        readonly List<MuseumGuest> guests=new List<MuseumGuest>();
        readonly System.Random random=new System.Random();
        VisitorCatalog catalog;
        ArtifactDisplay[] exhibits;
        GalleryDisplay gallery;
        MuseumSession session;
        Font font;
        float spawnWait;
        int room=-1,sequence;
        bool simulation;
        VisitorPreferencesFile preferencesFile;
        VisitorPreferences preferences=VisitorPreferences.Default();
        string settingsFingerprint,settingsError;
        float settingsPoll;
        public float SpeedMultiplier=>preferences.speed;
        public ReactionCatalog Reactions {get;private set;}
        public bool ShowReactions {get;set;}=true;
        public string NextPersona()=>ReactionCatalog.Personas[random.Next(ReactionCatalog.Personas.Length)];
        public bool CanShowThought(MuseumGuest guest)
        {
            if(Camera.main==null)return false;
            var camera=Camera.main;var point=guest.transform.position+Vector3.up*1.9f;
            if(Vector3.Distance(camera.transform.position,point)>7f || Vector3.Dot(camera.transform.forward,point-camera.transform.position)<=0)return false;
            return guests.Where(g=>g!=null&&g.gameObject.activeInHierarchy&&g.State==GuestState.Viewing&&!string.IsNullOrEmpty(g.ReactionText)&&Vector3.Distance(camera.transform.position,g.transform.position+Vector3.up*1.9f)<=7f&&Vector3.Dot(camera.transform.forward,g.transform.position+Vector3.up*1.9f-camera.transform.position)>0).OrderBy(g=>(g.transform.position-camera.transform.position).sqrMagnitude).Take(2).Contains(guest);
        }
        public bool BlocksPointer(Vector2 screenPoint)
        {
            if(gallery!=null&&gallery.IsOutside)return true;
            if(session==null || !session.desktopRig.activeInHierarchy) return false;
            return new Rect(Mathf.Max(20,Screen.width-280),120,260,185).Contains(new Vector2(screenPoint.x,Screen.height-screenPoint.y));
        }
        void Awake()
        {
            catalog=Resources.Load<VisitorCatalog>("VisitorCatalog");
            session=GetComponent<MuseumSession>();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},48);
            font.material.shader=Resources.Load<Shader>("WorldText");
            preferencesFile=new VisitorPreferencesFile(LocalArchive.FindRoot());
            Reactions=new ReactionCatalog(LocalArchive.FindRoot());
            ReloadPreferences();
        }
        void Update()
        {
            if(simulation) return;
            settingsPoll-=Time.unscaledDeltaTime;
            if(settingsPoll<=0) { settingsPoll=1; ReloadPreferences(); }
            gallery ??=GetComponent<GalleryDisplay>();
            if(gallery==null || !gallery.IsFinished || gallery.IsLoading) return;
            if(room!=gallery.CurrentRoom) PrepareRoom();
            Tick(Mathf.Min(.1f,Time.deltaTime));
        }
        void PrepareRoom()
        {
            if(OrderedTours){PrepareTours();return;}
            ClearRoom();
            room=gallery.CurrentRoom;
            exhibits=FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None).Where(s=>s.managedSlot).OrderBy(s=>s.transform.position.z).ThenBy(s=>s.transform.position.x).ToArray();
            var floor=GameObject.Find("Floor").GetComponent<Renderer>().bounds;
            Paths=new VisitorPaths(floor,exhibits);
        }
        public void SetCount(int count)
        {
            TargetCount=Mathf.Clamp(count,0,VisitorPreferences.MaxCount);
            while(guests.Count>TargetCount) { var last=guests[guests.Count-1]; guests.RemoveAt(guests.Count-1); last.Close(); }
        }
        void Tick(float dt)
        {
            if(Paths==null) return;
            spawnWait-=dt;
            if(guests.Count<TargetCount && spawnWait<=0)
            {
                Vector3 entrance=OrderedTours?TourNavigation.Door(TourNavigation.Get(TourRooms[0]).entrance):new Vector3(-1.8f,0,-4.1f);
                if(guests.Where(g=>!OrderedTours||g.TourRoom==0).All(g=>(g.transform.position-entrance).sqrMagnitude>.8f*.8f))
                {
                    Spawn(entrance); spawnWait=2.5f;
                }
            }
            for(int i=guests.Count-1;i>=0;i--)
            {
                var guest=guests[i]; guest.Tick(dt);
                if(guest.State!=GuestState.Finished) continue;
                if(guest.Aborted) Aborted++; else Exited++;
                guest.Close(); guests.RemoveAt(i);
            }
        }
        MuseumGuest Spawn(Vector3 position,VisitorAppearance selected=null)
        {
            var appearance=selected??PickAppearance();
            if(appearance==null) return null;
            var root=new GameObject("Museum guest "+(++sequence)); root.transform.position=position;
            var guest=root.AddComponent<MuseumGuest>();
            if(!guest.Initialize(this,appearance,exhibits,font,sequence))
            { guest.Close(); Debug.LogWarning("VISITOR_APPEARANCE_FAILED: "+appearance.id); return null; }
            guests.Add(guest); Entered++; return guest;
        }
        bool AppearanceEnabled(VisitorAppearance appearance)
        {
            if(appearance==null || !appearance.IsUsable) return false;
            var value=preferences.Find(appearance.id);
            return value==null ? appearance.weight>0 : value.enabled && value.weight>0;
        }
        VisitorAppearance PickAppearance(System.Random choiceRandom=null)
        {
            if(catalog==null) return null;
            var options=catalog.GetAvailable().Where(AppearanceEnabled).ToArray();
            var fresh=options.Where(p=>guests.All(g=>g.Visual.Appearance.id!=p.id)).ToArray();
            if(fresh.Length>0) options=fresh;
            double total=0; foreach(var entry in options) total+=preferences.Find(entry.id)?.weight??entry.weight;
            double pick=(choiceRandom??random).NextDouble()*total;
            foreach(var entry in options) { pick-=preferences.Find(entry.id)?.weight??entry.weight; if(pick<=0) return entry; }
            return options.LastOrDefault();
        }
        void ReloadPreferences()
        {
            if(!preferencesFile.TryRead(out var next,out var fingerprint,out var error))
            {
                if(settingsError!=error) {settingsError=error;Debug.LogWarning("VISITOR_SETTINGS_REJECTED: "+error);}
                return; // Keep the last valid settings when a damaged or partially written file is encountered.
            }
            settingsError=null;
            if(fingerprint==settingsFingerprint) return;
            preferences=next; settingsFingerprint=fingerprint;
            for(int i=guests.Count-1;i>=0;i--) if(!AppearanceEnabled(guests[i].Visual.Appearance))
            {guests[i].Close();guests.RemoveAt(i);}
            SetCount(next.enabled?next.count:0);
            Debug.Log($"VISITOR_SETTINGS_APPLIED: enabled={next.enabled} count={TargetCount} speed={SpeedMultiplier}");
        }
        public void RecordView() { Views++; }
        IEnumerable<Vector3> Bodies(MuseumGuest current)
        {
            foreach(var guest in guests) if(guest!=current && (!OrderedTours||guest.TourRoom==current.TourRoom)) yield return guest.transform.position;
            if(Camera.main!=null && !(gallery?.IsOutside??false) && (!OrderedTours||current.TourRoom==VisibleRoom)) { Vector3 player=Camera.main.transform.position; player.y=0; yield return player; }
        }
        public List<Vector3> FindPath(MuseumGuest guest,Vector3 destination)=>(OrderedTours?TourPath(guest.TourRoom):Paths).Find(guest.transform.position,destination,Bodies(guest));
        public bool CanMove(MuseumGuest current,Vector3 next)
        {
            if(!(OrderedTours?TourPath(current.TourRoom):Paths).Clear(next)) return false;
            foreach(var body in Bodies(current))
                if((next-body).sqrMagnitude<.65f*.65f && (next-body).sqrMagnitude<=(current.transform.position-body).sqrMagnitude) return false;
            return true;
        }
        public void ClearRoom()
        {
            foreach(var guest in guests) if(guest!=null) guest.Close(); guests.Clear(); tourQueues.Clear(); yieldingGuests.Clear(); Paths=null; room=-1; spawnWait=0; Reactions?.Clear();
        }
        void OnGUI()
        {
            if((gallery!=null&&gallery.IsOutside) || !session.desktopRig.activeInHierarchy || (GetComponent<ArtifactDetailsPanel>()?.IsOpen??false)) return;
            var style=new GUIStyle(GUI.skin.label){font=font,fontSize=15,alignment=TextAnchor.MiddleCenter};
            var button=new GUIStyle(GUI.skin.button){font=font,fontSize=15};
            float x=Mathf.Max(20,Screen.width-280);
            GUI.Box(new Rect(x,120,260,185),"");
            GUI.Label(new Rect(x+5,124,250,25),"관람객 · 타이쿤 실험",style);
            if(GUI.Button(new Rect(x+15,157,42,30),"−",button)) SetCount(TargetCount-1);
            GUI.Label(new Rect(x+60,157,135,30),$"{guests.Count(g=>g.gameObject.activeInHierarchy)}명 · 전체 {ActiveCount}/{TargetCount}",style);
            if(GUI.Button(new Rect(x+203,157,42,30),"+",button)) SetCount(TargetCount+1);
            if(GUI.Button(new Rect(x+15,201,90,28),TargetCount==0?"켜기":"모두 끄기",button)) SetCount(TargetCount==0?1:0);
            GUI.Label(new Rect(x+110,200,140,30),$"관람 {Views} · 퇴장 {Exited}",style);
            ShowReactions=GUI.Toggle(new Rect(x+15,271,230,25),ShowReactions,"감상 반응 표시",button);
            GUI.Label(new Rect(x+5,237,250,28),"앱 버튼은 임시 · 저장은 관리페이지",new GUIStyle(style){fontSize=12});
        }
        void OnDestroy() { ClearRoom(); if(font!=null) { Destroy(font.material); Destroy(font); } }

        public IEnumerator RunDiversitySmoke()
        {
            simulation=true; gallery=GetComponent<GalleryDisplay>();PrepareRoom();ClearRoom();
            var saved=preferences;preferences=VisitorPreferences.Default();
            var entries=catalog.GetAvailable();var generated=entries.Where(p=>p.id.StartsWith("quaternius-men-") || p.id.StartsWith("quaternius-women-")).ToArray();
            foreach(var p in catalog.appearances.Where(p=>p!=null && !p.IsUsable).Take(2)) Debug.Log($"VISITOR_CATALOG_DIAG: {p.id} prefab={p.prefab} animators={(p.prefab==null?-1:p.prefab.GetComponentsInChildren<Animator>(true).Length)} idle={p.idle} walk={p.walk} idleLength={(p.idle==null?-1:p.idle.length)} walkLength={(p.walk==null?-1:p.walk.length)} height={p.height} available={p.available} weight={p.weight}");
            SmokePassed=generated.Length==100 && generated.Select(p=>p.id).Distinct().Count()==100;
            var counts=entries.ToDictionary(p=>p.id,p=>0);var seeded=new System.Random(10492);
            for(int i=0;i<12000;i++) {var chosen=PickAppearance(seeded);if(chosen==null){SmokePassed=false;break;}counts[chosen.id]++;}
            SmokePassed &= counts.Values.All(n=>n>0);
            Debug.Log($"VISITOR_RANDOM_CHECK: draws=12000 selected={counts.Values.Count(n=>n>0)} available={entries.Count} min={counts.Values.Min()} max={counts.Values.Max()}");
            PrepareRoom();SetCount(0);
            for(int i=0;i<6;i++) Spawn(new Vector3(-3+i,0,0));
            SmokePassed &= guests.Count==6 && guests.Select(g=>g.Visual.Appearance.id).Distinct().Count()==6;
            ClearRoom();
            var ordered=generated.Where(p=>p.id.StartsWith("quaternius-men-")).OrderBy(p=>p.id).Zip(generated.Where(p=>p.id.StartsWith("quaternius-women-")).OrderBy(p=>p.id),(a,b)=>new[]{a,b}).SelectMany(pair=>pair).ToArray();
            int animated=0;
            for(int i=0;i<ordered.Length;i++)
            {
                var root=new GameObject("Diversity preview "+ordered[i].id);root.transform.position=new Vector3(i%10*1.6f,-(i/10)*2.3f,0);
                var visual=root.AddComponent<VisitorVisual>();bool valid=visual.Initialize(ordered[i]);
                root.transform.rotation=Quaternion.Euler(0,180,0);
                if(valid)
                {
                    var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();
                    var before=skins.Select(s=>{var mesh=new Mesh();s.BakeMesh(mesh);var vertices=mesh.vertices;Destroy(mesh);return vertices;}).ToArray();
                    visual.Tick(true,.3f);float delta=0;
                    for(int s=0;s<skins.Length;s++){var mesh=new Mesh();skins[s].BakeMesh(mesh);var vertices=mesh.vertices;for(int v=0;v<vertices.Length;v++)delta=Mathf.Max(delta,Vector3.Distance(vertices[v],before[s][v]));Destroy(mesh);}
                    var posed=visual.PoseBounds;var offset=posed.center-root.transform.position;
                    bool check=skins.Length==4 && delta>.0001f && posed.size.y>.5f && posed.size.y<3 && new Vector2(offset.x,offset.z).magnitude<.3f && Mathf.Abs(posed.min.y-root.transform.position.y)<.15f && visual.Renderers.All(r=>r.sharedMaterials.All(m=>m!=null && m.shader!=null && m.shader.isSupported));
                    if(check)animated++;SmokePassed &= check;
                    Debug.Log($"VISITOR_VARIANT_CHECK: {ordered[i].id} animated={delta} size={posed.size} offset={offset} passed={check}");
                    visual.Tick(false,1f);
                }
                else SmokePassed=false;
                var label=new GameObject("Label");label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,-.12f,0);label.transform.localRotation=Quaternion.Euler(0,180,0);
                var text=label.AddComponent<TextMesh>();text.font=font;text.fontSize=48;text.characterSize=.003f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.2f,.25f,.23f);text.text=ordered[i].displayName;label.GetComponent<Renderer>().sharedMaterial=font.material;
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
                if(i%5==0)yield return null;
            }
            foreach(var camera in Camera.allCameras)camera.enabled=false;
            var previewCamera=new GameObject("Diversity contact sheet camera").AddComponent<Camera>();previewCamera.transform.position=new Vector3(7.2f,-9.45f,-30);previewCamera.transform.rotation=Quaternion.identity;
            previewCamera.orthographic=true;previewCamera.orthographicSize=12;previewCamera.cullingMask=1<<30;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.94f,.94f,.9f);previewCamera.farClipPlane=80;
            preferences=saved;
            session.desktopRig.SetActive(false);gallery.enabled=false;enabled=false;
            Debug.Log($"VISITOR_DIVERSITY_CHECK: tested={generated.Length} animated={animated}");
            Debug.Log(SmokePassed?"VISITOR_DIVERSITY_SMOKE_PASS":"VISITOR_DIVERSITY_SMOKE_FAIL");
        }

        public IEnumerator RunPreferencesSmoke()
        {
            simulation=true; gallery=GetComponent<GalleryDisplay>(); PrepareRoom();
            var file=preferencesFile.FilePath;
            SmokePassed=System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file),".visitor-test-fixture"));
            if(!SmokePassed) {Debug.Log("VISITOR_PREFERENCES_SMOKE_FAIL: isolated fixture required");yield break;}
            var original=System.IO.File.Exists(file)?System.IO.File.ReadAllText(file):null;
            SmokePassed &= preferencesFile.TryRead(out var initial,out _,out _) && TargetCount==(initial.enabled?initial.count:0) && Mathf.Abs(SpeedMultiplier-initial.speed)<.0001f;
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-museum-visitor-settings-startup-only")>=0)
            {Debug.Log(SmokePassed?"VISITOR_PREFERENCES_SMOKE_PASS":"VISITOR_PREFERENCES_SMOKE_FAIL");yield break;}
            try
            {
                var appearance=catalog.GetAvailable()[0];
                var next=VisitorPreferences.Default(); next.count=3; next.speed=.7f;
                next.appearances=catalog.GetAvailable().Select(p=>new VisitorAppearancePreference{id=p.id,enabled=p.id==appearance.id,weight=2}).ToArray();
                void Save(VisitorPreferences value){System.IO.File.WriteAllText(file,JsonUtility.ToJson(value));ReloadPreferences();}
                Save(next); SmokePassed &= TargetCount==3 && Mathf.Abs(SpeedMultiplier-.7f)<.001f;
                Tick(.1f); SmokePassed &= ActiveCount==1;
                SetCount(6); ReloadPreferences(); SmokePassed &= TargetCount==6; // Temporary app adjustment remains until the saved file changes.
                next.count=12; Save(next); SmokePassed &= TargetCount==12;
                next.count=13; Save(next); SmokePassed &= TargetCount==12;
                SetCount(999); SmokePassed &= TargetCount==12;
                SetCount(-1); SmokePassed &= TargetCount==0;
                next.count=3; Save(next);
                System.IO.File.WriteAllText(file,JsonUtility.ToJson(next));
                System.IO.File.SetLastWriteTimeUtc(file,System.DateTime.UtcNow.AddSeconds(1)); ReloadPreferences();
                SmokePassed &= TargetCount==3; // Saving identical values must still override a temporary adjustment.
                next.appearances[0].enabled=false; Save(next); Tick(3);
                SmokePassed &= ActiveCount==0;
                next.appearances=new[]{new VisitorAppearancePreference{id="removed-character",enabled=true,weight=100}};
                Save(next); Tick(3); SmokePassed &= ActiveCount==1; // Unknown old IDs never become spawnable; the current catalog still supplies defaults.
                next.enabled=false; Save(next); SmokePassed &= TargetCount==0 && ActiveCount==0;
                next.speed=2; Save(next); SmokePassed &= TargetCount==0 && Mathf.Abs(SpeedMultiplier-.7f)<.001f;
                System.IO.File.WriteAllText(file,"{"); ReloadPreferences(); SmokePassed &= TargetCount==0;
                System.IO.File.WriteAllText(file,new string(' ',65537)); ReloadPreferences(); SmokePassed &= TargetCount==0;
                next.speed=.7f; next.appearances=new[]{new VisitorAppearancePreference{id=appearance.id,enabled=true,weight=1},new VisitorAppearancePreference{id=appearance.id,enabled=true,weight=1}};
                Save(next); SmokePassed &= TargetCount==0;
            }
            finally
            {
                if(original==null) System.IO.File.Delete(file); else System.IO.File.WriteAllText(file,original);
                ReloadPreferences(); ClearRoom();
            }
            yield return null;
            SmokePassed &= preferencesFile.TryRead(out var restored,out _,out _) && TargetCount==(restored.enabled?restored.count:0);
            Debug.Log(SmokePassed?"VISITOR_PREFERENCES_SMOKE_PASS":"VISITOR_PREFERENCES_SMOKE_FAIL");
        }

        public IEnumerator RunSmoke()
        {
            simulation=true; gallery=GetComponent<GalleryDisplay>(); PrepareRoom();
            SmokePassed=catalog!=null && catalog.GetAvailable().Count>=1;
            if(!SmokePassed) { Debug.Log("VISITOR_SMOKE_FAIL: catalog"); yield break; }
            var profile=catalog.GetAvailable()[0];
            // Profile-only extension: a second appearance, disabled appearance, duplicate ID and missing clips.
            var extra=Instantiate(profile); extra.id="runtime-extension-test"; extra.weight=2;
            var broken=ScriptableObject.CreateInstance<VisitorAppearance>(); broken.id="broken";
            var saved=catalog.appearances.ToList();
            try
            {
                catalog.appearances.Clear(); catalog.appearances.Add(profile); catalog.appearances.Add(profile); catalog.appearances.Add(extra); catalog.appearances.Add(broken);
                SmokePassed &= catalog.GetAvailable().Count==2;
                extra.available=false; SmokePassed &= catalog.GetAvailable().Count==1; extra.available=true;
                profile.available=false; SmokePassed &= catalog.Pick(new System.Random(1))==extra; profile.available=true;
                SetCount(0);
                var guest=Spawn(new Vector3(-1.8f,0,-4.1f),extra);
                SmokePassed &= guest!=null && guest.Visual.Appearance.id==extra.id;
                if(guest!=null)
                {
                    // A real skinned mesh must deform during playback and remain human-sized/visible.
                    var skin=guest.Visual.GetComponentInChildren<SkinnedMeshRenderer>();
                    SmokePassed &= skin!=null;
                    if(skin!=null)
                    {
                        var mesh=new Mesh(); skin.BakeMesh(mesh); var before=mesh.vertices;
                        guest.Visual.Tick(true,.3f); skin.BakeMesh(mesh); var after=mesh.vertices;
                        float delta=0; for(int i=0;i<before.Length;i++) delta=Mathf.Max(delta,Vector3.Distance(before[i],after[i]));
                        Bounds bounds=guest.Visual.Bounds;
                        bool animated=delta>.0001f && bounds.size.y>.5f && bounds.size.y<3f && bounds.size.magnitude<5;
                        SmokePassed &= animated;
                        Debug.Log($"VISITOR_ANIMATION_CHECK: delta={delta} bounds={bounds.size} passed={animated}");
                        Destroy(mesh);
                    }
                    Vector3 player=Camera.main.transform.position; player.y=0;
                    SmokePassed &= !CanMove(guest,player);
                    int previousExit=Exited,previousViews=Views;
                    for(int n=0;n<1600 && guests.Contains(guest);n++)
                    {
                        Tick(.1f);
                        SmokePassed &= Paths.Clear(guest.transform.position);
                        if(n%20==0) yield return null;
                    }
                    bool cycle=Exited>previousExit && (exhibits.Any(s=>s.Loaded)?Views>previousViews:true);
                    SmokePassed &= cycle;
                    Debug.Log($"VISITOR_CYCLE_CHECK: entered={Entered} views={Views} exited={Exited} aborted={Aborted} passed={cycle}");
                }
                SetCount(6);
                for(int n=0;n<180;n++) { Tick(.1f); if(n%20==0) yield return null; }
                SmokePassed &= ActiveCount>0 && ActiveCount<=6;
                SetCount(0); yield return null;
                SmokePassed &= ActiveCount==0 && FindObjectsByType<MuseumGuest>(FindObjectsSortMode.None).Length==0;
                if(gallery.RoomCount>1)
                {
                    SetCount(1); Tick(.1f);
                    var transition=gallery.SwitchRoomAsync((gallery.CurrentRoom+1)%gallery.RoomCount);
                    while(!transition.IsCompleted) yield return null;
                    yield return null;
                    SmokePassed &= !transition.IsFaulted && ActiveCount==0 && FindObjectsByType<MuseumGuest>(FindObjectsSortMode.None).Length==0;
                    transition=gallery.SwitchRoomAsync(0); while(!transition.IsCompleted) yield return null;
                    SmokePassed &= !transition.IsFaulted;
                }
            }
            finally
            {
                profile.available=true; catalog.appearances=saved; Destroy(extra); Destroy(broken); ClearRoom();
            }
            PrepareRoom(); SetCount(1);
            var preview=Spawn(new Vector3(.9f,0,.1f));
            if(preview!=null) { preview.transform.rotation=Quaternion.Euler(0,200,0); preview.Visual.Tick(false,.02f); }
            Debug.Log(SmokePassed?"VISITOR_SMOKE_PASS":"VISITOR_SMOKE_FAIL");
            // Freeze the scripted preview for screenshot capture; production updates resume on a normal launch.
        }
    }
}
