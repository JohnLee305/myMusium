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
            int crowdTarget=6,peak=0,lateViews=0,lateExits=0;
            var smokeArgs=Environment.GetCommandLineArgs();int countArg=Array.IndexOf(smokeArgs,"-museum-tour-count");
            if(countArg>=0 && (countArg+1>=smokeArgs.Length || !int.TryParse(smokeArgs[countArg+1],out crowdTarget) || crowdTarget<1 || crowdTarget>VisitorPreferences.MaxCount))
            { SmokePassed=false;Debug.Log("TOUR_SMOKE_FAIL: invalid crowd target");yield break; }
            ClearRoom();PrepareTours();SetCount(crowdTarget);
            bool cancelledWaiter=false,cancelledOwner=false,queuePreview=false;
            for(int i=0;i<6000;i++)
            {
                Tick(.1f);peak=Mathf.Max(peak,guests.Count);SmokePassed &= guests.Count<=crowdTarget && QueueChecksPassed;
                if(i==4800){lateViews=Views;lateExits=Exited;}
                foreach(var g in guests)
                {
                    SmokePassed &= TourPath(g.TourRoom).Clear(g.transform.position) && g.TourVisited.SequenceEqual(expected.Take(g.TourVisited.Count));
                    foreach(var other in guests)
                        if(g!=other && g.TourRoom==other.TourRoom)SmokePassed &= Vector3.Distance(g.transform.position,other.transform.position)>=.64f;
                }
                if(i>300 && !cancelledWaiter)
                {
                    var removed=guests.FirstOrDefault(g=>g.State==GuestState.Waiting);
                    if(removed!=null){removed.Close();guests.Remove(removed);SmokePassed &= !HasQueueReferences(removed.name);cancelledWaiter=true;}
                }
                if(i>600 && !cancelledOwner)
                {
                    var removed=guests.FirstOrDefault(g=>g.State==GuestState.Viewing);
                    if(removed!=null){removed.Close();guests.Remove(removed);SmokePassed &= !HasQueueReferences(removed.name);cancelledOwner=true;}
                }
                if(!queuePreview && guests.Any(g=>g.TourRoom==VisibleRoom&&g.State==GuestState.Viewing) && guests.Count(g=>g.State==GuestState.Waiting)>=2)
                {
                    queuePreview=true;
                    var args=Environment.GetCommandLineArgs();int imageArg=Array.IndexOf(args,"-museum-screenshot");
                    if(imageArg>=0 && imageArg+1<args.Length && Camera.main!=null)
                    {
                        var camera=Camera.main.transform;var savedPosition=camera.position;var savedRotation=camera.rotation;
                        var subject=guests.First(g=>g.TourRoom==VisibleRoom&&g.State==GuestState.Viewing).transform.position;
                        camera.position=new Vector3(Mathf.Clamp(subject.x,-6,6),7,Mathf.Clamp(subject.z-4,-3,11));camera.LookAt(subject);
                        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(args[imageArg+1]+".queue.png");
                        for(int frame=0;frame<5;frame++)yield return null;
                        camera.SetPositionAndRotation(savedPosition,savedRotation);
                    }
                }
                if(i%100==0)yield return null;
            }
            SmokePassed &= peak==crowdTarget && Entered>1&&(expected.Length==0?Views==0:Views>expected.Length)&&Exited>0&&Aborted==0;
            bool progress=(expected.Length==0 || Views>lateViews) && Exited>lateExits;
            SmokePassed &= progress;
            Debug.Log($"TOUR_RECENT_PROGRESS: views={Views-lateViews} exits={Exited-lateExits} passed={progress}");
            // Moving the player's camera between rooms must not replace the museum's guests.
            var retained=guests.ToArray();var tickets=retained.Select(g=>g.QueueTicket).ToArray();
            if(gallery.RoomCount>1){var task=gallery.SwitchRoomAsync(1);while(!task.IsCompleted)yield return null;PrepareTours();SmokePassed &= !task.IsFaulted&&retained.All(g=>guests.Contains(g))&&retained.Select(g=>g.QueueTicket).SequenceEqual(tickets);}
            foreach(var g in guests)Debug.Log("TOUR_GUEST_STATE: "+g.name+" "+g.TourStatus);
            SmokePassed &= QueueChecksPassed && (expected.Length==0 || (QueueDepartures>0 && MaxQueueDepth>=2 && cancelledWaiter && cancelledOwner));
            Debug.Log($"TOUR_QUEUE_REMOVAL: waiter={cancelledWaiter} owner={cancelledOwner}");
            Debug.Log($"TOUR_QUEUE_CHECK: waits={QueueWaits} maxDepth={MaxQueueDepth} departures={QueueDepartures} passed={QueueChecksPassed}");
            Debug.Log($"TOUR_CROWD_CHECK: target={crowdTarget} peak={peak} entered={Entered} views={Views} exited={Exited} aborted={Aborted} active={guests.Count} passed={SmokePassed}");
            Debug.Log(SmokePassed?"TOUR_SMOKE_PASS":"TOUR_SMOKE_FAIL");
        }
    }
}
