using System;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumGuest
    {
        public int TourRoom {get;private set;}
        public string TourStatus=>$"room={TourRoom} state={State} position={transform.position} index={tourIndex} waypoint={waypoint}/{path?.Count} destination={(path==null?Vector3.zero:path[path.Count-1])}";
        int tourIndex;
        ArtifactData tourTarget;
        Vector3 tourCenter;
        float retry;
        bool tourLeaving;
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
                foreach(var offset in TourNavigation.ViewOffsets)
                {
                    if(!SetPath(tourCenter+offset))continue;
                    tourTarget=item;State=GuestState.Walking;return;
                }
                // A guest occupying a viewing spot is temporary: wait and retry this same exhibit.
                State=GuestState.Walking;path=null;retry=1;return;
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
            var nav=TourNavigation.Get(museum.TourRooms[TourRoom]);
            transform.position=TourNavigation.Door(nav.entrance);State=GuestState.Entering;
            SetPath(transform.position+TourNavigation.Inward(nav.entrance)*.5f);
        }
        void TickTour(float dt)
        {
            if(State==GuestState.Finished)return;
            gameObject.SetActive(TourRoom==museum.VisibleRoom);
            bool moving=false;
            if(State==GuestState.Viewing)
            {
                wait-=dt;thoughtTime-=dt;
                var direction=tourCenter-transform.position;direction.y=0;
                if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*150);
                if(wait<=0)NextTourTarget();
            }
            else if(path==null)
            {
                retry-=dt;
                if(retry<=0){if(tourLeaving){SetPath(TourNavigation.Door(TourNavigation.Get(museum.TourRooms[TourRoom]).exit));retry=1;}else NextTourTarget();}
            }
            else if(waypoint>=path.Count)
            {
                if(State==GuestState.Entering)NextTourTarget();
                else if(State==GuestState.Leaving)AdvanceTourRoom();
                else if(tourTarget!=null)
                {
                    State=GuestState.Viewing;wait=4.5f+(tourIndex%3)*.5f;Viewed++;museum.RecordView();tourIndex++;
                    TourVisited.Add(tourTarget.id);
                    SetReactionText(museum.Reactions.Select(tourTarget,Persona,reactionHistory));
                    Debug.Log($"TOUR_VIEW: guest={name} room={museum.TourRooms[TourRoom].id} artifact={tourTarget.id} number={tourIndex}");
                }
            }
            else
            {
                var next=Vector3.MoveTowards(transform.position,path[waypoint],Visual.Appearance.walkSpeed*museum.SpeedMultiplier*dt);
                if(!museum.CanMove(this,next))
                {
                    blockedFor+=dt;
                    if(blockedFor>=2){var destination=path[path.Count-1];if(!SetPath(destination)){path=null;retry=1;}blockedFor=0;}
                }
                else
                {
                    blockedFor=0;var direction=next-transform.position;moving=direction.sqrMagnitude>.000001f;
                    if(moving)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*210);
                    transform.position=next;if(Vector3.Distance(next,path[waypoint])<.01f)waypoint++;
                }
            }
            Visual.Tick(moving,dt,museum.SpeedMultiplier);UpdateThoughtVisibility();
        }
    }
}
