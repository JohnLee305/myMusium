using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed class MuseumVisitors : MonoBehaviour
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
        public bool BlocksPointer(Vector2 screenPoint)
        {
            if(session==null || !session.desktopRig.activeInHierarchy) return false;
            return new Rect(Mathf.Max(20,Screen.width-280),120,260,125).Contains(new Vector2(screenPoint.x,Screen.height-screenPoint.y));
        }
        void Awake()
        {
            catalog=Resources.Load<VisitorCatalog>("VisitorCatalog");
            session=GetComponent<MuseumSession>();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},48);
            font.material.shader=Resources.Load<Shader>("WorldText");
        }
        void Update()
        {
            if(simulation) return;
            gallery ??=GetComponent<GalleryDisplay>();
            if(gallery==null || !gallery.IsFinished || gallery.IsLoading) { ClearRoom(); return; }
            if(room!=gallery.CurrentRoom) PrepareRoom();
            Tick(Mathf.Min(.1f,Time.deltaTime));
        }
        void PrepareRoom()
        {
            ClearRoom();
            room=gallery.CurrentRoom;
            exhibits=FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None).Where(s=>s.managedSlot).OrderBy(s=>s.transform.position.z).ThenBy(s=>s.transform.position.x).ToArray();
            var floor=GameObject.Find("Floor").GetComponent<Renderer>().bounds;
            Paths=new VisitorPaths(floor,exhibits);
        }
        public void SetCount(int count)
        {
            TargetCount=Mathf.Clamp(count,0,6);
            while(guests.Count>TargetCount) { var last=guests[guests.Count-1]; guests.RemoveAt(guests.Count-1); last.Close(); }
        }
        void Tick(float dt)
        {
            if(Paths==null) return;
            spawnWait-=dt;
            if(guests.Count<TargetCount && spawnWait<=0)
            {
                Vector3 entrance=new Vector3(-1.8f,0,-4.1f);
                if(guests.All(g=>(g.transform.position-entrance).sqrMagnitude>.8f*.8f))
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
            var appearance=selected??catalog?.Pick(random);
            if(appearance==null) return null;
            var root=new GameObject("Museum guest "+(++sequence)); root.transform.position=position;
            var guest=root.AddComponent<MuseumGuest>();
            if(!guest.Initialize(this,appearance,exhibits,font,sequence))
            { guest.Close(); Debug.LogWarning("VISITOR_APPEARANCE_FAILED: "+appearance.id); return null; }
            guests.Add(guest); Entered++; return guest;
        }
        public void RecordView() { Views++; }
        IEnumerable<Vector3> Bodies(MuseumGuest current)
        {
            foreach(var guest in guests) if(guest!=current) yield return guest.transform.position;
            if(Camera.main!=null) { Vector3 player=Camera.main.transform.position; player.y=0; yield return player; }
        }
        public List<Vector3> FindPath(MuseumGuest guest,Vector3 destination)=>Paths.Find(guest.transform.position,destination,Bodies(guest));
        public bool CanMove(MuseumGuest current,Vector3 next)
        {
            if(!Paths.Clear(next)) return false;
            foreach(var body in Bodies(current))
                if((next-body).sqrMagnitude<.65f*.65f && (next-body).sqrMagnitude<=(current.transform.position-body).sqrMagnitude) return false;
            return true;
        }
        public void ClearRoom()
        {
            foreach(var guest in guests) if(guest!=null) guest.Close(); guests.Clear(); Paths=null; room=-1; spawnWait=0;
        }
        void OnGUI()
        {
            if(!session.desktopRig.activeInHierarchy || (GetComponent<ArtifactDetailsPanel>()?.IsOpen??false)) return;
            var style=new GUIStyle(GUI.skin.label){font=font,fontSize=15,alignment=TextAnchor.MiddleCenter};
            var button=new GUIStyle(GUI.skin.button){font=font,fontSize=15};
            float x=Mathf.Max(20,Screen.width-280);
            GUI.Box(new Rect(x,120,260,125),"");
            GUI.Label(new Rect(x+5,124,250,25),"관람객 · 타이쿤 실험",style);
            if(GUI.Button(new Rect(x+15,157,42,30),"−",button)) SetCount(TargetCount-1);
            GUI.Label(new Rect(x+60,157,135,30),$"{ActiveCount}명 / 목표 {TargetCount}명",style);
            if(GUI.Button(new Rect(x+203,157,42,30),"+",button)) SetCount(TargetCount+1);
            if(GUI.Button(new Rect(x+15,201,90,28),TargetCount==0?"켜기":"모두 끄기",button)) SetCount(TargetCount==0?1:0);
            GUI.Label(new Rect(x+110,200,140,30),$"관람 {Views} · 퇴장 {Exited}",style);
        }
        void OnDestroy() { ClearRoom(); if(font!=null) { Destroy(font.material); Destroy(font); } }

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
