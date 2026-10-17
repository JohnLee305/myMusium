using UnityEngine;

namespace FamilyMuseum
{
    public static class CrowdCapacity
    {
        public const int MaxAutomatic = 120;
        public static int RoomLimit(MuseumRoom room)
        {
            int free = 30 - TourNavigation.Get(room).blockedCells.Length - room.cells.Length;
            return Mathf.Clamp(Mathf.Min(free + 1, 4 + 2*room.artifacts.Length), 6, 12);
        }
        public static int Target(MuseumRoom[] rooms)
        {
            int target = 0;
            if (rooms != null) foreach (var room in rooms)
                if (room.artifacts.Length > 0) target += Mathf.Min(6, RoomLimit(room));
            return Mathf.Min(MaxAutomatic, target);
        }
    }
    public sealed partial class MuseumVisitors
    {
        public bool AutomaticPopulation { get; private set; }
        public bool AdmissionPaused => admissionPause > 0;
        float slowFrames, admissionPause;
        readonly System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<MuseumGuest>> roomGuests = new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<MuseumGuest>>();
        System.Collections.Generic.List<MuseumGuest> GuestsInRoom(int index)
        {
            if (!roomGuests.TryGetValue(index, out var list)) roomGuests[index] = list = new System.Collections.Generic.List<MuseumGuest>();
            return list;
        }
        public void ChangeGuestRoom(MuseumGuest guest, int previous)
        {
            GuestsInRoom(previous).Remove(guest); GuestsInRoom(guest.TourRoom).Add(guest);
        }
        void ApplyPopulation()
        {
            AutomaticPopulation = preferences.mode == "auto";
            SetTarget(preferences.enabled ? (AutomaticPopulation ? CrowdCapacity.Target(TourRooms) : preferences.count) : 0);
        }
        void SetTarget(int count)
        {
            TargetCount = Mathf.Clamp(count, 0, AutomaticPopulation ? CrowdCapacity.MaxAutomatic : VisitorPreferences.MaxCount);
            while (guests.Count > TargetCount) { var last = guests[guests.Count-1]; guests.RemoveAt(guests.Count-1); last.Close(); }
        }
        void ObserveFrame(float dt)
        {
            admissionPause = Mathf.Max(0, admissionPause-dt);
            // Only new arrivals are delayed; established visitors keep their tickets and tour.
            slowFrames = dt > 1f/45 ? slowFrames+Mathf.Min(dt,.1f) : Mathf.Max(0,slowFrames-dt*2);
            if (slowFrames > .5f) admissionPause = 2;
        }
        bool HasRoomCapacity(int index) => !AutomaticPopulation || GuestsInRoom(index).Count < CrowdCapacity.RoomLimit(TourRooms[index]);
        int pathsRemaining;
        bool limitPaths;
        public int PeakPathSearches { get; private set; }
        readonly System.Collections.Generic.Dictionary<MuseumGuest,long> deferredRoutes = new System.Collections.Generic.Dictionary<MuseumGuest,long>();
        long routeOrder;
        readonly MuseumGuest[] tickGuests=new MuseumGuest[MaxGuests];
        const int MaxGuests=CrowdCapacity.MaxAutomatic;
        System.Collections.Generic.IComparer<MuseumGuest> tickComparer;
        int CompareUpdates(MuseumGuest a,MuseumGuest b)
        {
            long left=deferredRoutes.TryGetValue(a,out var x)?x:long.MaxValue;
            long right=deferredRoutes.TryGetValue(b,out var y)?y:long.MaxValue;
            int order=left.CompareTo(right);return order==0?a.Sequence.CompareTo(b.Sequence):order;
        }
    }
}
