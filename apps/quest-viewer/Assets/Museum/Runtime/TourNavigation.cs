using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum
{
    [Serializable] public sealed class MuseumDoor { public string side; public int slot; }
    [Serializable] public sealed class RoomNavigation { public MuseumDoor entrance,exit; public int[] blockedCells; public string[] visitOrder; }
    public static class TourNavigation
    {
        public static readonly Bounds Floor=new Bounds(new Vector3(0,-.1f,4.5f),new Vector3(16.4f,.2f,19));
        public static Vector3 Center(int cell)=>new Vector3((cell%6-2.5f)*2.4f,0,cell/6*2.8f);
        public static Vector3 Door(MuseumDoor d)=>d.side=="front"||d.side=="back"?new Vector3((d.slot-2.5f)*2.4f,0,d.side=="front"?-4.1f:13.1f):new Vector3(d.side=="left"?-7.3f:7.3f,0,d.slot*2.8f);
        public static Vector3 Inward(MuseumDoor d)=>d.side=="front"?Vector3.forward:d.side=="back"?Vector3.back:d.side=="left"?Vector3.right:Vector3.left;
        public static Rect Block(int cell) {var p=Center(cell);float minX=cell%6==0?-8.2f:p.x-1.2f,maxX=cell%6==5?8.2f:p.x+1.2f,minZ=cell/6==0?-5f:p.z-1.4f,maxZ=cell/6==4?14f:p.z+1.4f;return Rect.MinMaxRect(minX,minZ,maxX,maxZ);}
        public static Vector3[] ViewOffsets={Vector3.back*1.15f,Vector3.left*1.15f,Vector3.right*1.15f,Vector3.forward*1.15f};
        public static RoomNavigation Get(MuseumRoom room)=>room.navigation??new RoomNavigation{entrance=new MuseumDoor{side="front",slot=2},exit=new MuseumDoor{side="right",slot=0},blockedCells=Array.Empty<int>(),visitOrder=room.artifacts.OrderBy(p=>room.cells[Array.IndexOf(room.artifacts,p)]).ToArray()};
        public static VisitorPaths Paths(MuseumRoom room)
        {
            var obstacles=new List<Rect>();
            foreach(int c in room.cells){var p=Center(c)+Vector3.forward*.8f;obstacles.Add(new Rect(p.x-.85f,p.z-.85f,1.7f,1.7f));}
            foreach(int c in Get(room).blockedCells){var b=Block(c);obstacles.Add(new Rect(b.xMin-.3f,b.yMin-.3f,b.width+.6f,b.height+.6f));}
            return new VisitorPaths(Floor,obstacles);
        }
        // JsonUtility instantiates absent nested objects: inspect actual JSON keys before validation.
        public static void ReadPresence(MuseumManifest manifest,string json)
        {
            var raw=(Dictionary<string,object>)new StrictReactionJson(json.TrimStart('\uFEFF'),true).Read();
            if(!raw.ContainsKey("roomOrder"))manifest.roomOrder=null;
            else if(!(raw["roomOrder"] is List<object> order)||order.Any(v=>!(v is string)))throw new InvalidDataException("TOUR_ROOM_ORDER_INVALID");
            if(manifest.rooms==null||!raw.TryGetValue("rooms",out var roomsValue)||!(roomsValue is List<object> rooms))return;
            for(int i=0;i<manifest.rooms.Length;i++)
            {
                var r=(Dictionary<string,object>)rooms[i];
                if(!r.TryGetValue("navigation",out var value)){manifest.rooms[i].navigation=null;continue;}
                var keys=new[]{"entrance","exit","blockedCells","visitOrder"};
                if(!(value is Dictionary<string,object> n)||n.Count!=4||keys.Any(k=>!n.ContainsKey(k)))throw new InvalidDataException("TOUR_NAVIGATION_INVALID");
                foreach(string key in new[]{"entrance","exit"})if(!(n[key] is Dictionary<string,object> d)||d.Count!=2||!d.ContainsKey("side")||!d.ContainsKey("slot")||!(d["side"] is string)||!(d["slot"] is double slot)||slot!=Math.Truncate(slot))throw new InvalidDataException("TOUR_DOOR_INVALID");
                if(!(n["blockedCells"] is List<object> cells)||cells.Any(v=>!(v is double number)||number!=Math.Truncate(number))||!(n["visitOrder"] is List<object> entries)||entries.Any(v=>!(v is string)))throw new InvalidDataException("TOUR_NAVIGATION_INVALID");
            }
        }
        public static void Validate(MuseumRoom room)
        {
            var n=room.navigation;if(n==null)return;
            foreach(var d in new[]{n.entrance,n.exit})if(d==null||!new[]{"front","back","left","right"}.Contains(d.side)||d.slot<0||d.slot>=(d.side=="front"||d.side=="back"?6:5))throw new InvalidDataException("TOUR_DOOR_INVALID");
            if(n.entrance.side==n.exit.side&&n.entrance.slot==n.exit.slot)throw new InvalidDataException("TOUR_DOOR_OVERLAP");
            if(n.blockedCells==null||n.blockedCells.Length>30||n.blockedCells.Distinct().Count()!=n.blockedCells.Length||n.blockedCells.Any(c=>c<0||c>=30||room.cells.Contains(c)))throw new InvalidDataException("TOUR_BLOCKED_INVALID");
            if(n.visitOrder==null||n.visitOrder.Length!=room.artifacts.Length||n.visitOrder.Distinct().Count()!=n.visitOrder.Length||n.visitOrder.Any(p=>!room.artifacts.Contains(p)))throw new InvalidDataException("TOUR_ORDER_INVALID");
            var paths=Paths(room);var entry=Door(n.entrance);
            if(paths.Find(entry,Door(n.exit))==null)throw new InvalidDataException("TOUR_EXIT_UNREACHABLE");
            foreach(int cell in room.cells)if(!ViewOffsets.Any(offset=>paths.Find(entry,Center(cell)+Vector3.forward*.8f+offset)!=null))throw new InvalidDataException("TOUR_EXHIBIT_UNREACHABLE");
        }
    }
}
