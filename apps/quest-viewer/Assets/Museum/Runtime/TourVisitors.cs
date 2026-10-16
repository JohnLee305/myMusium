using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumVisitors
    {
        public MuseumRoom[] TourRooms {get;private set;}
        public int VisibleRoom=>gallery==null?0:gallery.CurrentRoom;
        bool tourSmoke;
        public bool OrderedTours=>!simulation||tourSmoke;
        readonly Dictionary<int,VisitorPaths> tourPaths=new Dictionary<int,VisitorPaths>();
        readonly Dictionary<string,ArtifactData> tourArtifacts=new Dictionary<string,ArtifactData>();
        public VisitorPaths TourPath(int index)
        {
            if(!tourPaths.TryGetValue(index,out var paths))tourPaths[index]=paths=TourNavigation.Paths(TourRooms[index]);
            return paths;
        }
        public ArtifactData TourArtifact(string entry)
        {
            if(tourArtifacts.TryGetValue(entry,out var item))return item;
            try{var archive=new LocalArchive(LocalArchive.FindRoot());var loaded=archive.ReadArtifact(entry);archive.Resolve(loaded.model);return tourArtifacts[entry]=loaded;}catch(Exception){tourArtifacts[entry]=null;return null;}
        }
        public bool CanEnterTourRoom(MuseumGuest current,int index) {var p=TourNavigation.Door(TourNavigation.Get(TourRooms[index]).entrance);return guests.All(g=>g==current||g.TourRoom!=index||(g.transform.position-p).sqrMagnitude>.8f*.8f);}
        void PrepareTours()
        {
            if(TourRooms==null){TourRooms=new LocalArchive(LocalArchive.FindRoot()).ReadRooms();tourPaths.Clear();tourArtifacts.Clear();}
            exhibits ??= Array.Empty<ArtifactDisplay>();room=gallery.CurrentRoom;Paths=TourPath(room);
        }
        public IEnumerator RunTourSmoke()
        {
            simulation=true;tourSmoke=true;gallery=GetComponent<GalleryDisplay>();ClearRoom();PrepareTours();SetCount(0);
            SmokePassed=true;
            foreach(string kind in new[]{"valid","invalid"}){var folder=System.IO.Path.Combine(LocalArchive.FindRoot(),"route-contract-"+kind);if(System.IO.Directory.Exists(folder))foreach(string contract in System.IO.Directory.GetDirectories(folder)){bool accepted=true;try{new LocalArchive(contract).ReadRooms();}catch(Exception error){accepted=false;Debug.Log("TOUR_CONTRACT_REJECT: "+System.IO.Path.GetFileName(contract)+" "+error.Message);}bool pass=accepted==(kind=="valid");SmokePassed &= pass;Debug.Log($"TOUR_CONTRACT: {kind}/{System.IO.Path.GetFileName(contract)} passed={pass}");}}
            var expected=TourRooms.SelectMany(r=>TourNavigation.Get(r).visitOrder).Select(p=>TourArtifact(p)?.id).Where(id=>id!=null).ToArray();
            var guest=Spawn(TourNavigation.Door(TourNavigation.Get(TourRooms[0]).entrance));
            SmokePassed &= guest!=null;
            if(guest!=null)
            {
                for(int i=0;i<25000&&guest.State!=GuestState.Finished;i++){guest.Tick(.1f);SmokePassed &= TourPath(guest.TourRoom).Clear(guest.transform.position);if(i%100==0)yield return null;}
                SmokePassed &= guest.State==GuestState.Finished&&!guest.Aborted&&guest.TourVisited.SequenceEqual(expected)&&guest.TourRoom==TourRooms.Length-1;
                Debug.Log($"TOUR_SEQUENCE_CHECK: expected={string.Join(",",expected)} actual={string.Join(",",guest.TourVisited)} rooms={TourRooms.Length} passed={SmokePassed}");
            }
            ClearRoom();PrepareTours();SetCount(6);
            for(int i=0;i<6000;i++){Tick(.1f);SmokePassed &= guests.Count<=6;foreach(var g in guests)SmokePassed &= TourPath(g.TourRoom).Clear(g.transform.position);if(i%100==0)yield return null;}
            SmokePassed &= Entered>1&&(expected.Length==0?Views==0:Views>expected.Length)&&Exited>0&&Aborted==0;
            // Moving the player's camera between rooms must not replace the museum's guests.
            var retained=guests.ToArray();if(gallery.RoomCount>1){var task=gallery.SwitchRoomAsync(1);while(!task.IsCompleted)yield return null;PrepareTours();SmokePassed &= !task.IsFaulted&&retained.All(g=>guests.Contains(g));}
            foreach(var g in guests)Debug.Log("TOUR_GUEST_STATE: "+g.name+" "+g.TourStatus);
            Debug.Log($"TOUR_CROWD_CHECK: entered={Entered} views={Views} exited={Exited} aborted={Aborted} active={guests.Count} passed={SmokePassed}");
            Debug.Log(SmokePassed?"TOUR_SMOKE_PASS":"TOUR_SMOKE_FAIL");
        }
    }
}
