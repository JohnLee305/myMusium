using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    public enum GuestState { Entering, Walking, Viewing, Leaving, Finished }
    public sealed class MuseumGuest : MonoBehaviour
    {
        public GuestState State { get; private set; }
        public VisitorVisual Visual { get; private set; }
        public int Viewed { get; private set; }
        public bool Aborted { get; private set; }
        MuseumVisitors museum;
        readonly List<ArtifactDisplay> targets=new List<ArtifactDisplay>();
        List<Vector3> path;
        ArtifactDisplay target;
        int waypoint,targetIndex;
        float wait,blockedFor,lifetime;
        TextMesh thought;
        public bool Initialize(MuseumVisitors owner,VisitorAppearance appearance,ArtifactDisplay[] exhibits,Font font,int sequence)
        {
            museum=owner;
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
            var label=new GameObject("Visitor thought"); label.transform.SetParent(transform,false);
            label.transform.localPosition=Vector3.up*(appearance.height+.15f);
            thought=label.AddComponent<TextMesh>(); thought.font=font; thought.fontSize=48; thought.characterSize=.004f;
            thought.anchor=TextAnchor.MiddleCenter; thought.alignment=TextAlignment.Center;
            thought.GetComponent<Renderer>().sharedMaterial=font.material;
            State=GuestState.Entering;
            SetPath(new Vector3(-1.8f,0,-2.3f));
            return true;
        }
        bool SetPath(Vector3 destination)
        {
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
            if(State==GuestState.Finished) return;
            lifetime+=dt;
            if(lifetime>180) { Aborted=true; State=GuestState.Finished; Visual.Tick(false,dt); return; }
            bool moving=false;
            if(State==GuestState.Viewing)
            {
                wait-=dt;
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
                    thought.text="어떤 이야기가 담겨 있을까요?";
                }
                else if(State==GuestState.Leaving) State=GuestState.Finished;
            }
            else
            {
                Vector3 next=Vector3.MoveTowards(transform.position,path[waypoint],Visual.Appearance.walkSpeed*dt);
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
            Visual.Tick(moving,dt);
            if(thought!=null && Camera.main!=null) thought.transform.rotation=Quaternion.LookRotation(thought.transform.position-Camera.main.transform.position);
        }
        public void Close() { if(this==null) return; if(Visual!=null) Visual.Close(); gameObject.SetActive(false); Destroy(gameObject); }
    }
}
