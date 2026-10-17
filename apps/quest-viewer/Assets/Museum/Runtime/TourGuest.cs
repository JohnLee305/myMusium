using System;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumGuest
    {
        public int TourRoom {get;private set;}
        public string TourStatus=>$"room={TourRoom} state={State} position={transform.position} index={tourIndex} ticket={QueueTicket} yielding={tourYielding} blocked={blockedFor} waypoint={waypoint}/{path?.Count} destination={(path==null||path.Count==0?Vector3.zero:path[path.Count-1])}";
        int tourIndex;
        ArtifactData tourTarget;
        Vector3 tourCenter;
        float retry;
        bool tourLeaving;
        TourExhibitQueue tourQueue;
        public long QueueTicket { get; internal set; }
        bool tourYielding;
        internal MuseumGuest YieldPriority {get;set;}
        internal bool RouteDeferred {get;set;}
        float yieldPause,yieldCooldown,yieldAge;
        internal bool FollowingQueuePoint(Vector3 point)=>path!=null && path.Count>0 && waypoint<path.Count && (path[path.Count-1]-point).sqrMagnitude<.0001f;
        internal bool TourStalled=>State!=GuestState.Viewing && State!=GuestState.Finished && !tourYielding &&
            ((State!=GuestState.Waiting && path==null) || blockedFor>=1.5f);
        Vector3 resumeDestination;
        bool TryYield(Vector3 destination)
        {
            if(tourYielding || yieldCooldown>0 || !museum.TryTourYield(this,out var escape))return false;
            resumeDestination=destination;path=escape;waypoint=0;blockedFor=0;tourYielding=true;yieldPause=1.2f;yieldCooldown=3;yieldAge=0;
            return true;
        }
        bool SetQueuePath(Vector3 destination)
        {
            AuditRoute();
            path=museum.QueueRoute(this,destination);waypoint=0;blockedFor=0;
            return path!=null;
        }
        public readonly List<string> TourVisited=new List<string>();
        public void BeginTour()
        {
            TourRoom=0;tourIndex=0;
            transform.position=TourNavigation.Door(museum.TourRooms[0].navigation?.entrance??TourNavigation.Get(museum.TourRooms[0]).entrance);
            State=GuestState.Entering;
            SetPath(transform.position+TourNavigation.Inward(TourNavigation.Get(museum.TourRooms[0]).entrance)*.5f);
        }
        void NextTourTarget()
        {
            var room=museum.TourRooms[TourRoom];var nav=TourNavigation.Get(room);
            tourTarget=null;retry=0;
            while(tourIndex<nav.visitOrder.Length)
            {
                string entry=nav.visitOrder[tourIndex];int i=Array.IndexOf(room.artifacts,entry);
                ArtifactData item=museum.TourArtifact(entry);
                if(item==null){Debug.LogWarning("TOUR_ARTIFACT_SKIPPED: "+entry);tourIndex++;continue;}
                tourCenter=TourNavigation.Center(room.cells[i])+Vector3.forward*.8f;
                tourTarget=item;
                tourQueue=museum.JoinTourQueue(this,entry,tourCenter);
                State=GuestState.Waiting;path=null;retry=0;return;
            }
            tourLeaving=true;State=GuestState.Leaving;
            if(!SetPath(TourNavigation.Door(nav.exit))){path=null;retry=1;}
        }
        void AdvanceTourRoom()
        {
            Debug.Log($"TOUR_DOOR_CROSSED: guest={name} room={museum.TourRooms[TourRoom].id}");
            if(TourRoom+1>=museum.TourRooms.Length){State=GuestState.Finished;return;}
            if(!museum.CanEnterTourRoom(this,TourRoom+1))return;
            TourRoom++;tourIndex=0;tourLeaving=false;tourTarget=null;
            museum.ChangeGuestRoom(this,TourRoom-1);
            var nav=TourNavigation.Get(museum.TourRooms[TourRoom]);
            transform.position=TourNavigation.Door(nav.entrance);State=GuestState.Entering;
            SetPath(transform.position+TourNavigation.Inward(nav.entrance)*.5f);
        }
        void TickTour(float dt)
        {
            if(State==GuestState.Finished)return;
            RouteDeferred=false;
            gameObject.SetActive(TourRoom==museum.VisibleRoom);
            bool moving=false;
            var motionStart=transform.position;
            yieldCooldown-=dt;
            if(tourYielding)
            {
                yieldAge+=dt;
                if(yieldAge>=8)
                {
                    museum.FinishTourYield(this);tourYielding=false;motionSpeed=0;SetQueuePath(resumeDestination);retry=1;
                    Visual.Tick(false,dt,museum.SpeedMultiplier);return;
                }
            }
            if(tourYielding && (path==null || waypoint>=path.Count))
            {
                yieldPause-=dt;
                bool passed=YieldPriority==null || YieldPriority.State==GuestState.Finished || YieldPriority.TourRoom!=TourRoom ||
                    Vector3.Distance(YieldPriority.transform.position,resumeDestination)>1.8f;
                if(yieldPause<=0 && passed){museum.FinishTourYield(this);tourYielding=false;motionSpeed=0;SetQueuePath(resumeDestination);retry=1;}
                Visual.Tick(false,dt,museum.SpeedMultiplier);UpdateThoughtVisibility();return;
            }
            if(!tourYielding && State==GuestState.Waiting && retry<=0)
            {
                if(tourQueue==null)NextTourTarget();
                if(tourQueue!=null && tourQueue.Tickets.TryAcquire(name))
                {
                    tourQueue.Waiting.Remove(name);
                    State=GuestState.Walking;SetQueuePath(tourQueue.View);retry=1;
                }
                else if(tourQueue!=null)
                {
                    if(museum.TryWaitingPath(this,tourQueue,out var waitingPath)){path=waitingPath;waypoint=0;}
                    if(path==null || waypoint>=path.Count)
                    {
                        var resume=tourQueue.Waiting.TryGetValue(name,out var parked)?parked:transform.position;
                        TryYield(resume);
                    }
                    retry=1;
                }
                else retry=1;
            }
            retry-=dt;
            if(State==GuestState.Viewing)
            {
                wait-=dt;thoughtTime-=dt;
                var direction=tourCenter-transform.position;direction.y=0;
                if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*150);
                if(wait<=0){State=GuestState.Clearing;SetQueuePath(tourQueue.Clear);retry=1;}
            }
            else if(path==null)
            {
                if(retry<=0)
                {
                    if(State==GuestState.Waiting) { retry=0; }
                    else if(State==GuestState.Clearing){if(!SetQueuePath(tourQueue.Clear))TryYield(tourQueue.Clear);retry=1;}
                    else if(State==GuestState.Walking && tourQueue!=null){if(!SetQueuePath(tourQueue.View))TryYield(tourQueue.View);retry=1;}
                    else if(tourLeaving){var exit=TourNavigation.Door(TourNavigation.Get(museum.TourRooms[TourRoom]).exit);if(!SetPath(exit))TryYield(exit);retry=1;}
                    else NextTourTarget();
                }
            }
            else if(waypoint>=path.Count)
            {
                if(State==GuestState.Entering)NextTourTarget();
                else if(State==GuestState.Leaving)AdvanceTourRoom();
                else if(State==GuestState.Clearing)
                {
                    museum.ReleaseTourPlace(this,tourQueue);tourQueue=null;NextTourTarget();
                }
                else if(State==GuestState.Waiting){path=null;retry=1;}
                else if(State==GuestState.Walking && tourTarget!=null)
                {
                    museum.RecordQueuedView(this,tourQueue);
                    State=GuestState.Viewing;wait=4.5f+(tourIndex%3)*.5f;Viewed++;museum.RecordView();tourIndex++;
                    TourVisited.Add(tourTarget.id);
                    SetReactionText(museum.Reactions.Select(tourTarget,Persona,reactionHistory));
                    Debug.Log($"TOUR_VIEW: guest={name} room={museum.TourRooms[TourRoom].id} artifact={tourTarget.id} number={tourIndex}");
                }
            }
            else
            {
                moving=MoveTour(dt);
            }
            if(!moving && (path==null || waypoint>=path.Count || State==GuestState.Viewing))motionSpeed=Mathf.MoveTowards(motionSpeed,0,dt*3);
            float actualSpeed=Vector3.Distance(motionStart,transform.position)/Mathf.Max(.0001f,dt);
            Visual.TickLocomotion(actualSpeed/Mathf.Max(.1f,Visual.Appearance.walkSpeed*museum.SpeedMultiplier),dt,museum.SpeedMultiplier);UpdateThoughtVisibility();
            if(RouteDeferred)retry=0; // Budget deferral retries next frame, not the blocked-path timer.
        }
    }
}
