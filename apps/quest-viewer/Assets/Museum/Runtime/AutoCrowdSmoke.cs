using System;
using System.Collections;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumGuest
    {
        internal void PlaceForLoadTest(int index, Vector3 position)
        {
            museum.RemoveTourQueueGuest(this);
            int old=TourRoom;TourRoom=index;tourIndex=0;tourQueue=null;
            museum.ChangeGuestRoom(this,old);
            transform.position=position;State=GuestState.Entering;
            SetPath(position+TourNavigation.Inward(TourNavigation.Get(museum.TourRooms[index]).entrance)*.5f);
        }
    }
    public sealed partial class MuseumVisitors
    {
        bool loadSeeded;
        void SeedCrowdLoad()
        {
            if(loadSeeded)return;loadSeeded=true;
            var args=Environment.GetCommandLineArgs();int a=Array.IndexOf(args,"-museum-crowd-load");
            if(a<0 || !auditing)return;
            if(!int.TryParse(args[a+1],out int count) || count<1 || count>120 || TourRooms.Length*6<count)throw new InvalidOperationException("Invalid load fixture");
            // Explicit performance fixture only: distribute six visitors per open room.
            // Normal operation always admits sequentially through the first entrance.
            for(int i=0;i<count;i++)
            {
                int index=i/6;var nav=TourNavigation.Get(TourRooms[index]);
                var position=TourNavigation.Door(nav.entrance)+TourNavigation.Inward(nav.entrance)*(i%6)*.95f;
                var guest=Spawn(position);guest.PlaceForLoadTest(index,position);
            }
            Debug.Log($"AUTO_LOAD_SEEDED: count={guests.Count} rooms={TourRooms.Length}");
        }
        public IEnumerator RunAutoCrowdSmoke()
        {
            simulation=true;tourSmoke=true;gallery=GetComponent<GalleryDisplay>();
            ClearRoom();PrepareTours();preferences.mode="auto";preferences.enabled=true;ApplyPopulation();
            SmokePassed=TargetCount==Mathf.Min(120,TourRooms.Length*6);
            var empty=new MuseumRoom{artifacts=Array.Empty<string>(),cells=Array.Empty<int>()};
            SmokePassed &= CrowdCapacity.Target(Array.Empty<MuseumRoom>())==0 && CrowdCapacity.Target(new[]{empty})==0;
            var many=new MuseumRoom[25];for(int i=0;i<many.Length;i++)many[i]=TourRooms[0];
            SmokePassed &= CrowdCapacity.Target(many)==120;
            int target=TargetCount,peak=0,previousViews=Views,previousExits=Exited,lateViews=0,lateExits=0;
            bool geometry=true,spacing=true,capacity=true;
            // Sustained overload delays admissions, and recovery resumes without removing guests.
            for(int i=0;i<60;i++)ObserveFrame(.04f);
            SmokePassed &= AdmissionPaused;
            for(int i=0;i<600;i++)ObserveFrame(.007f);
            SmokePassed &= !AdmissionPaused;
            for(int i=0;i<54000;i++)
            {
                Tick(1f/60);peak=Mathf.Max(peak,guests.Count);
                SmokePassed &= TargetCount==target && guests.Count<=target && QueueChecksPassed;
                for(int r=0;r<TourRooms.Length;r++)capacity &= GuestsInRoom(r).Count<=CrowdCapacity.RoomLimit(TourRooms[r]);
                foreach(var guest in guests)
                {
                    geometry &= TourPath(guest.TourRoom).Clear(guest.transform.position);
                    foreach(var other in GuestsInRoom(guest.TourRoom))
                        if(other!=guest)spacing &= (guest.transform.position-other.transform.position).sqrMagnitude>=.64f*.64f;
                }
                if(i==46800){lateViews=Views;lateExits=Exited;}
                if(i%120==0)yield return null;
            }
            SmokePassed &= capacity && geometry && spacing && Views>previousViews && Exited>previousExits && Views>lateViews && Exited>lateExits && PeakPathSearches<=2;
            Debug.Log($"AUTO_SAFETY: capacity={capacity} geometry={geometry} spacing={spacing}");
            foreach(var guest in guests)Debug.Log("AUTO_STATE: "+guest.name+" "+guest.TourStatus);
            // Returning to manual count and switching back must keep room indices coherent.
            SetCount(4);SmokePassed &= guests.Count<=4;
            ApplyPopulation();SmokePassed &= TargetCount==target;
            int indexed=0;foreach(var list in roomGuests.Values)indexed+=list.Count;
            SmokePassed &= indexed==guests.Count;
            Debug.Log($"AUTO_CROWD_CHECK: target={target} peak={peak} views={Views-previousViews} exits={Exited-previousExits} lateViews={Views-lateViews} lateExits={Exited-lateExits} pathPeak={PeakPathSearches} passed={SmokePassed}");
            Debug.Log(SmokePassed?"AUTO_CROWD_SMOKE_PASS":"AUTO_CROWD_SMOKE_FAIL");
        }
    }
}
