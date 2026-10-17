using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    public enum GuestState { Entering, Walking, Viewing, Leaving, Finished, Waiting, Clearing }
    public sealed partial class MuseumGuest : MonoBehaviour
    {
        public GuestState State { get; private set; }
        public VisitorVisual Visual { get; private set; }
        public int Viewed { get; private set; }
        public bool Aborted { get; private set; }
        public int Sequence { get; private set; }
        MuseumVisitors museum;
        readonly List<ArtifactDisplay> targets=new List<ArtifactDisplay>();
        List<Vector3> path;
        ArtifactDisplay target;
        int waypoint,targetIndex;
        float wait,blockedFor,lifetime;
        TextMesh thought;
        Material thoughtBackground;
        GameObject bubble;
        static readonly Color InkColor=new Color(.14f,.19f,.17f);
        const float BalloonWidth=.85f;
        const float BalloonHeight=.68f;
        const float TextMaxWidth=.55f;
        const float TextMaxHeight=.30f;
        const float TextCenterY=.17f;
        float thoughtTime;
        readonly Queue<string> reactionHistory=new Queue<string>();
        public string Persona {get;internal set;}
        public string ReactionText {get;private set;}="";
        public string CurrentArtifactTitle=>tourTarget?.title??target?.Data?.title??"";
        public bool ThoughtVisible=>bubble!=null&&bubble.activeSelf;
        static string Wrap(string value)
        {
            var result=new System.Text.StringBuilder();int column=0;
            for(int i=0;i<value.Length;i++){result.Append(value[i]);if(char.IsHighSurrogate(value[i])&&i+1<value.Length)result.Append(value[++i]);if(++column>=10&&i+1<value.Length){result.Append('\n');column=0;}}
            return result.ToString();
        }
        internal void SetReactionText(string value)
        {
            ReactionText=value;thought.text=Wrap(value);thoughtTime=4.5f;
        }
        public void UpdateThoughtVisibility()
        {
            if(bubble==null)return;
            bool visible=gameObject.activeInHierarchy && museum.ShowReactions && State==GuestState.Viewing && thoughtTime>0 && !string.IsNullOrEmpty(ReactionText) && Camera.main!=null && museum.CanShowThought(this);
            bubble.SetActive(visible);
            if(visible)
            {
                bubble.transform.rotation=Quaternion.LookRotation(bubble.transform.position-Camera.main.transform.position);
                var bounds=thought.GetComponent<Renderer>().localBounds;
                if(bounds.size.x>.001f&&bounds.size.y>.001f)
                {
                    float scale=Mathf.Min(1,TextMaxWidth/bounds.size.x,TextMaxHeight/bounds.size.y);thought.transform.localScale=Vector3.one*scale;
                }
            }
        }
        public bool Initialize(MuseumVisitors owner,VisitorAppearance appearance,ArtifactDisplay[] exhibits,Font font,int sequence)
        {
            museum=owner; Persona=owner.NextPersona();
            Sequence=sequence;
            Visual=gameObject.AddComponent<VisitorVisual>();
            if(!Visual.Initialize(appearance)) return false;
            foreach(var exhibit in exhibits) if(exhibit.Loaded) targets.Add(exhibit);
            if(targets.Count>0)
            {
                // Rotate visitation order so several guests do not all choose the same exhibit first.
                int offset=sequence%targets.Count;
                for(int n=0;n<offset;n++) { var first=targets[0]; targets.RemoveAt(0); targets.Add(first); }
                if(targets.Count>3) targets.RemoveRange(3,targets.Count-3);
            }
            bubble=new GameObject("Visitor reaction bubble");bubble.transform.SetParent(transform,false);
            bubble.transform.localPosition=Vector3.up*(appearance.height+.3f);
            var label=new GameObject("Visitor thought"); label.transform.SetParent(bubble.transform,false);
            label.transform.localPosition=new Vector3(0,TextCenterY,0);
            var cute=Resources.Load<Font>("ThoughtFont");
            if(cute!=null) cute.material.shader=Resources.Load<Shader>("WorldText");
            thought=label.AddComponent<TextMesh>(); thought.font=cute!=null?cute:font; thought.fontSize=48; thought.characterSize=.06f; thought.richText=false; thought.color=InkColor; thought.fontStyle=FontStyle.Bold;
            thought.anchor=TextAnchor.MiddleCenter; thought.alignment=TextAlignment.Center;
            thought.GetComponent<Renderer>().sharedMaterial=thought.font.material;
            var background=GameObject.CreatePrimitive(PrimitiveType.Quad);background.name="Reaction background";background.transform.SetParent(bubble.transform,false);
            background.transform.localPosition=new Vector3(0,.08f,.012f);background.transform.localScale=new Vector3(BalloonWidth,BalloonHeight,1);
            Destroy(background.GetComponent<Collider>());thoughtBackground=new Material(Resources.Load<Shader>("ReactionBackground"));thoughtBackground.color=Color.white;thoughtBackground.mainTexture=Resources.Load<Texture2D>("ThoughtBalloon");background.GetComponent<Renderer>().sharedMaterial=thoughtBackground;
            bubble.SetActive(false);
            if(museum.OrderedTours){BeginTour();return true;}
            State=GuestState.Entering;
            SetPath(new Vector3(-1.8f,0,-2.3f));
            return true;
        }
        bool SetPath(Vector3 destination)
        {
            AuditRoute();
            path=museum.FindPath(this,destination); waypoint=0; blockedFor=0;
            return path!=null;
        }
        void NextExhibit()
        {
            target=null;
            while(targetIndex<targets.Count)
            {
                var next=targets[targetIndex++]; if(next==null || !next.Loaded) continue;
                Vector3 center=next.modelSlot.position; center.y=0;
                foreach(var offset in new[]{Vector3.back*1.05f,Vector3.left*1.1f,Vector3.right*1.1f,Vector3.forward*1.1f})
                {
                    if(!SetPath(center+offset)) continue;
                    target=next; State=GuestState.Walking; thought.text=""; return;
                }
            }
            Leave();
        }
        void Leave()
        {
            State=GuestState.Leaving; thought.text="";
            if(!SetPath(new Vector3(1.8f,0,-4.1f))) { Aborted=true; State=GuestState.Finished; }
        }
        public void Tick(float dt)
        {
            if(museum.OrderedTours){TickTour(dt);return;}
            if(State==GuestState.Finished) return;
            lifetime+=dt;
            if(lifetime>180) { Aborted=true; State=GuestState.Finished; Visual.Tick(false,dt); return; }
            bool moving=false;
            if(State==GuestState.Viewing)
            {
                wait-=dt;thoughtTime-=dt;
                if(target!=null)
                {
                    Vector3 direction=target.modelSlot.position-transform.position; direction.y=0;
                    if(direction.sqrMagnitude>.001f) transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*150);
                }
                if(wait<=0) NextExhibit();
            }
            else if(path==null) { Aborted=true; State=GuestState.Finished; }
            else if(waypoint>=path.Count)
            {
                if(State==GuestState.Entering) NextExhibit();
                else if(State==GuestState.Walking)
                {
                    State=GuestState.Viewing; wait=4.5f; Viewed++; museum.RecordView();
                    SetReactionText(museum.Reactions.Select(target?.Data,Persona,reactionHistory));
                }
                else if(State==GuestState.Leaving) State=GuestState.Finished;
            }
            else
            {
                Vector3 next=Vector3.MoveTowards(transform.position,path[waypoint],Visual.Appearance.walkSpeed*museum.SpeedMultiplier*dt);
                if(!museum.CanMove(this,next))
                {
                    blockedFor+=dt;
                    if(blockedFor>5)
                    {
                        if(State==GuestState.Leaving) { if(!SetPath(path[path.Count-1])) { Aborted=true; State=GuestState.Finished; } }
                        else NextExhibit();
                    }
                }
                else
                {
                    blockedFor=0; Vector3 direction=next-transform.position;
                    moving=direction.sqrMagnitude>.000001f;
                    if(moving) transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*210);
                    transform.position=next;
                    if(Vector3.Distance(next,path[waypoint])<.01f) waypoint++;
                }
            }
            Visual.Tick(moving,dt,museum.SpeedMultiplier);
            UpdateThoughtVisibility();
        }
        public void Close() { if(this==null) return; museum?.RemoveTourQueueGuest(this); if(Visual!=null) Visual.Close(); if(thoughtBackground!=null) Destroy(thoughtBackground); gameObject.SetActive(false); Destroy(gameObject); }
    }
}
