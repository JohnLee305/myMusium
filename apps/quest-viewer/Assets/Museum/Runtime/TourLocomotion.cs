using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumGuest
    {
        float motionSpeed;
        internal Vector3 TravelDirection=>path!=null && waypoint<path.Count ? (path[waypoint]-transform.position).normalized : Vector3.zero;
        internal bool HasTravelIntent=>State!=GuestState.Viewing && State!=GuestState.Finished && path!=null && waypoint<path.Count;
        bool MoveTour(float dt)
        {
            float fullSpeed=Visual.Appearance.walkSpeed*museum.SpeedMultiplier;
            // Consume coincident waypoints immediately, avoiding one idle frame at each corner.
            while(waypoint<path.Count && (path[waypoint]-transform.position).sqrMagnitude<.0001f)waypoint++;
            if(waypoint>=path.Count){motionSpeed=0;return false;}
            var direction=(path[waypoint]-transform.position).normalized;
            float distance=Vector3.Distance(transform.position,path[waypoint]);
            float target=fullSpeed*museum.TourPace(this,direction);
            if(waypoint==path.Count-1)target=Mathf.Min(target,Mathf.Sqrt(2*2.5f*distance));
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),dt*240);
            float facing=Mathf.Clamp01((Vector3.Dot(transform.forward,direction)+.1f)/1.1f);
            target*=facing;
            motionSpeed=Mathf.MoveTowards(motionSpeed,target,dt*(target<motionSpeed?3f:1.5f));
            var next=Vector3.MoveTowards(transform.position,path[waypoint],motionSpeed*dt);
            bool allowed=museum.CanMove(this,next);
            bool moving=allowed && (next-transform.position).sqrMagnitude>.0000000001f;
            if(!allowed || (target<.05f && distance>.05f))
            {
                // Preserve the stuck timer through restarts rather than repeatedly forgetting it.
                blockedFor+=dt;if(!allowed)motionSpeed=0;
                if(blockedFor>=2)
                {
                    var destination=path[path.Count-1];
                    if(!tourYielding && TryYield(destination))return false;
                    bool found=tourQueue!=null?SetQueuePath(destination):SetPath(destination);
                    if(!found){path=null;retry=1;}blockedFor=0;
                }
            }
            else
            {
                if(moving)blockedFor=0;
                transform.position=next;
                if(Vector3.Distance(next,path[waypoint])<.01f)waypoint++;
            }
            return moving;
        }
    }

    public sealed partial class MuseumVisitors
    {
        // Short-horizon right of way: older visitors retain priority at a crossing.
        // Following visitors slow before the hard body boundary, without occupying a new path.
        public float TourPace(MuseumGuest current,Vector3 direction)
        {
            float pace=1;
            foreach(var guest in guests)
            {
                if(guest==current || guest.TourRoom!=current.TourRoom)continue;
                var offset=guest.transform.position-current.transform.position;
                float ahead=Vector3.Dot(offset,direction);
                if(ahead<=0 || ahead>1.6f)continue;
                float sideways=(offset-direction*ahead).magnitude;
                if(sideways>.72f)continue;
                bool following=guest.HasTravelIntent && Vector3.Dot(direction,guest.TravelDirection)>.5f;
                bool crossing=guest.HasTravelIntent && !following;
                // Both visitors still keep the .65m safety boundary; priority affects approach speed.
                if(crossing && current.Sequence<guest.Sequence && guest.State!=GuestState.Clearing)continue;
                pace=Mathf.Min(pace,Mathf.Clamp01((ahead-.72f)/.75f));
            }
            if(Camera.main!=null && !(gallery?.IsOutside??false) && current.TourRoom==VisibleRoom)
            {
                var offset=Camera.main.transform.position-current.transform.position;offset.y=0;
                float ahead=Vector3.Dot(offset,direction);
                if(ahead>0 && (offset-direction*ahead).magnitude<.72f)pace=Mathf.Min(pace,Mathf.Clamp01((ahead-.72f)/.75f));
            }
            return pace;
        }
    }
}
