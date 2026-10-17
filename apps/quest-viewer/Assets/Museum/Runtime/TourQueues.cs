using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed class TourExhibitQueue
    {
        public readonly VisitorQueue Tickets = new VisitorQueue();
        public readonly Dictionary<string, Vector3> Waiting = new Dictionary<string, Vector3>();
        public int Room;
        public bool First;
        public Vector3 Center, View, Clear;
        public List<Vector3> ClearancePath;
        public List<Vector3> Approach;
        public List<Vector3> Places;
        public long LastViewed;
    }

    public sealed partial class MuseumVisitors
    {
        readonly Dictionary<string, TourExhibitQueue> tourQueues = new Dictionary<string, TourExhibitQueue>();
        readonly Dictionary<int, MuseumGuest> yieldingGuests = new Dictionary<int, MuseumGuest>();
        public int QueueWaitingPlans { get; private set; }
        public int QueueYields { get; private set; }
        public bool QueueChecksPassed { get; private set; } = true;
        public int QueueWaits { get; private set; }
        public int QueueDepartures { get; private set; }
        public int MaxQueueDepth { get; private set; }

        public TourExhibitQueue JoinTourQueue(MuseumGuest guest, string entry, Vector3 center)
        {
            // Prepare all lanes before allocating waiting places in this room.
            var room = TourRooms[guest.TourRoom];
            for (int i = 0; i < room.artifacts.Length; i++)
                GetTourQueue(guest.TourRoom, room.artifacts[i], TourNavigation.Center(room.cells[i]) + Vector3.forward * .8f);
            var q = GetTourQueue(guest.TourRoom, entry, center);
            if (q == null) return null;
            guest.QueueTicket = q.Tickets.Join(guest.name);
            MaxQueueDepth = Mathf.Max(MaxQueueDepth, q.Tickets.WaitingCount);
            return q;
        }

        TourExhibitQueue GetTourQueue(int roomIndex, string entry, Vector3 center)
        {
            string key = roomIndex + ":" + entry;
            if (!tourQueues.TryGetValue(key, out var q))
            {
                var paths = TourPath(roomIndex);
                var entrance = TourNavigation.Door(TourNavigation.Get(TourRooms[roomIndex]).entrance);
                // Pick one stable viewing place with a separate, reachable departure place.
                foreach (var offset in TourNavigation.ViewOffsets)
                {
                    var view = center + offset;
                    var approach = paths.Find(entrance, view);
                    if (approach == null) continue;
                    approach.Insert(0, entrance);
                    var tangent = new Vector3(-offset.z, 0, offset.x).normalized;
                    foreach (var delta in new[] { offset.normalized * 1.3f, tangent * 1.2f, -tangent * 1.2f })
                    {
                        var clear = view + delta;
                        if (!AwayFromDoors(roomIndex, clear, 1f)) continue;
                        var departure = paths.Find(view, clear);
                        if (departure == null) continue;
                        // A departing guest must not stand in the incoming line or need to
                        // cross the next viewer's body to leave a narrow dead-end corridor.
                        if (paths.Find(clear, entrance, new[] { view }) == null) continue;
                        bool separated = true;
                        for (int p = 1; p < approach.Count; p++)
                            if (SegmentDistance(clear, approach[p-1], approach[p]) < .9f) { separated = false; break; }
                        if (!separated) continue;
                        q = new TourExhibitQueue { Room = roomIndex, First = TourNavigation.Get(TourRooms[roomIndex]).visitOrder[0] == entry, Center = center, View = view, Clear = clear, ClearancePath = departure, Approach = approach };
                        break;
                    }
                    if (q != null) break;
                }
                if (q == null) return null; // Invalid space: retry without skipping the exhibit.
                tourQueues.Add(key, q);
            }
            return q;
        }

        bool AwayFromDoors(int roomIndex, Vector3 point, float radius)
        {
            var nav = TourNavigation.Get(TourRooms[roomIndex]);
            return (point - TourNavigation.Door(nav.entrance)).sqrMagnitude > radius * radius &&
                (point - TourNavigation.Door(nav.exit)).sqrMagnitude > radius * radius;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            var delta = b - a;
            float t = delta.sqrMagnitude < .0001f ? 0 : Mathf.Clamp01(Vector3.Dot(p - a, delta) / delta.sqrMagnitude);
            return Vector3.Distance(p, a + delta * t);
        }

        bool SafeWaitingPoint(TourExhibitQueue q, Vector3 point)
        {
            if (!TourPath(q.Room).Clear(point) || !AwayFromDoors(q.Room, point, 1.2f)) return false;
            // Keep all exhibits' approach spots free, including exhibits not yet visited.
            foreach (int cell in TourRooms[q.Room].cells)
            {
                var center = TourNavigation.Center(cell) + Vector3.forward * .8f;
                if ((point - center).sqrMagnitude < 1.25f * 1.25f) return false;
            }
            foreach (var other in tourQueues.Values)
            {
                if (other.Room != q.Room) continue;
                // Park beside circulation routes, never on the route needed by the oldest ticket.
                for(int i=1;i<other.Approach.Count;i++)
                    if(SegmentDistance(point,other.Approach[i-1],other.Approach[i])<.8f)return false;
                if ((point - other.View).sqrMagnitude < 1f || (point - other.Clear).sqrMagnitude < 1f) return false;
                var previous = other.View;
                foreach (var step in other.ClearancePath)
                {
                    if (SegmentDistance(point, previous, step) < .85f) return false;
                    previous = step;
                }
            }
            return true;
        }

        public bool TryWaitingPath(MuseumGuest guest, TourExhibitQueue q, out List<Vector3> route)
        {
            route = null;
            int rank = q.Tickets.WaitingRank(guest.name);
            if (rank < 0) return false;
            // Later exhibits receive guests from the preceding departure buffer. Walking back
            // to an entrance-based line would create opposing traffic in the same narrow lane.
            if(!q.First)return false;
            // Geometry is immutable for this tour. Plan once, not every parked guest/frame.
            if(q.Places==null)q.Places=PlanWaitingPlaces(q);
            if (rank >= q.Places.Count) return false;
            var destination = q.Places[rank];
            if (q.Waiting.TryGetValue(guest.name, out var reserved) && (reserved - destination).sqrMagnitude < .0001f)
            {
                if(guest.FollowingQueuePoint(destination))return false; // Keep the current path/waypoint.
                if((guest.transform.position-reserved).sqrMagnitude<.0001f)
                {route=new List<Vector3>();return true;}
            }
            route = QueueRoute(guest, destination);
            if (route == null) return false;
            if (!q.Waiting.ContainsKey(guest.name)) QueueWaits++;
            q.Waiting[guest.name] = destination;
            return true;
        }

        List<Vector3> PlanWaitingPlaces(TourExhibitQueue q)
        {
            QueueWaitingPlans++;
            var places=new List<Vector3>();
            var end = q.View;
            float distanceFromView = 0;
            float lastPlaced = 0;
            for (int i = q.Approach.Count - 1; i >= 0; i--)
            {
                var start = q.Approach[i];
                float length = Vector3.Distance(end, start);
                for (float d = .1f; d <= length; d += .1f)
                {
                    float along = distanceFromView + d;
                    if (along < 1.6f || along - lastPlaced < 1f) continue;
                    var center = Vector3.Lerp(end, start, d / length);
                    var direction=(start-end).normalized;
                    var side=new Vector3(-direction.z,0,direction.x);
                    foreach(var offset in new[]{side*.9f,-side*.9f,side*1.2f,-side*1.2f})
                    {
                        var point=center+offset;
                        if(!SafeWaitingPoint(q,point))continue;
                        bool separated=true;
                        foreach(var existing in places)if((point-existing).sqrMagnitude<.95f*.95f){separated=false;break;}
                        if(!separated)continue;
                        places.Add(point);lastPlaced=along;break;
                    }
                }
                distanceFromView += length;end = start;
            }
            return places;
        }

        public List<Vector3> QueueRoute(MuseumGuest guest, Vector3 destination) =>
            FindPath(guest, destination);

        public bool TryTourYield(MuseumGuest guest, out List<Vector3> route)
        {
            route=null;
            if(yieldingGuests.TryGetValue(guest.TourRoom,out var active) && active!=null && active!=guest)return false;
            // Older guests and guests already vacating a viewing place have priority.
            MuseumGuest priority=null;
            foreach(var other in guests)
                if(other!=guest && other.TourRoom==guest.TourRoom &&
                    (other.transform.position-guest.transform.position).sqrMagnitude<1.8f*1.8f &&
                    other.TourStalled && (other.Sequence<guest.Sequence || other.State==GuestState.Clearing))
                    if(priority==null || other.Sequence<priority.Sequence)priority=other;
            if(priority==null)return false;
            var escapePoints=new List<Vector3>();
            foreach(float radius in new[]{.8f,1.2f,1.6f})
                for(int i=0;i<8;i++)
                {
                    float angle=(i+(guest.Sequence%8))*Mathf.PI/4;
                    var point=guest.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    if(!AwayFromDoors(guest.TourRoom,point,.9f))continue;
                    // A yielding guest cannot occupy another exhibit's reserved approach place.
                    bool safe=true;
                    foreach(var q in tourQueues.Values)
                        if(q.Room==guest.TourRoom && ((point-q.View).sqrMagnitude<.85f*.85f || (point-q.Clear).sqrMagnitude<.85f*.85f)){safe=false;break;}
                    if(!safe)continue;
                    var priorityDirection=priority.TravelDirection.sqrMagnitude>.01f?priority.TravelDirection:priority.transform.forward;
                    if(SegmentDistance(point,priority.transform.position,priority.transform.position+priorityDirection*2.5f)<.9f)continue;
                    if(Vector3.Distance(point,priority.transform.position)<Vector3.Distance(guest.transform.position,priority.transform.position)+.2f)continue;
                    escapePoints.Add(point);
                }
            escapePoints.Sort((a,b)=>(a-guest.transform.position).sqrMagnitude.CompareTo((b-guest.transform.position).sqrMagnitude));
            foreach(var point in escapePoints)
            {
                route=FindPath(guest,point);
                if(route!=null){guest.YieldPriority=priority;yieldingGuests[guest.TourRoom]=guest;QueueYields++;return true;}
            }
            return false;
        }

        public void FinishTourYield(MuseumGuest guest)
        {
            if(yieldingGuests.TryGetValue(guest.TourRoom,out var active) && active==guest)yieldingGuests.Remove(guest.TourRoom);
        }

        public void RecordQueuedView(MuseumGuest guest, TourExhibitQueue q)
        {
            QueueChecksPassed &= q.Tickets.Owner == guest.name && guest.QueueTicket > q.LastViewed;
            q.LastViewed = guest.QueueTicket;
            Debug.Log($"TOUR_QUEUE_VIEW: guest={guest.name} ticket={guest.QueueTicket} room={q.Room} passed={QueueChecksPassed}");
        }

        public void ReleaseTourPlace(MuseumGuest guest, TourExhibitQueue q)
        {
            QueueChecksPassed &= Vector3.Distance(guest.transform.position, q.View) >= .9f && q.Tickets.Release(guest.name);
            q.Waiting.Remove(guest.name);
            QueueDepartures++;
        }

        public void RemoveTourQueueGuest(MuseumGuest guest)
        {
            FinishTourYield(guest);
            foreach (var q in tourQueues.Values)
            {
                q.Tickets.Cancel(guest.name);
                q.Waiting.Remove(guest.name);
            }
        }

        bool HasQueueReferences(string guest)
        {
            foreach(var q in tourQueues.Values)
                if(q.Tickets.Contains(guest) || q.Waiting.ContainsKey(guest))return true;
            return false;
        }
    }
}
