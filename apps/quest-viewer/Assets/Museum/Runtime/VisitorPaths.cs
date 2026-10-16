using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    // Floor navigation is rebuilt from this room's actual plinth colliders, not archive cell indices.
    public sealed class VisitorPaths
    {
        const float Step=.4f;
        readonly Rect floor;
        readonly List<Rect> obstacles=new List<Rect>();
        readonly int width,depth;
        readonly bool[] blocked;
        readonly List<Vector3> avoided=new List<Vector3>();
        Vector3 searchStart;
        public VisitorPaths(Bounds floorBounds,IEnumerable<ArtifactDisplay> exhibits):this(floorBounds,ExhibitObstacles(exhibits)) {}
        static IEnumerable<Rect> ExhibitObstacles(IEnumerable<ArtifactDisplay> exhibits)
        {
            foreach(var exhibit in exhibits)foreach(var collider in exhibit.GetComponentsInChildren<Collider>()){if(collider.isTrigger)continue;var b=collider.bounds;yield return new Rect(b.min.x-.3f,b.min.z-.3f,b.size.x+.6f,b.size.z+.6f);}
        }
        public VisitorPaths(Bounds floorBounds,IEnumerable<Rect> rectangles)
        {
            floor=new Rect(floorBounds.min.x+.55f,floorBounds.min.z+.55f,floorBounds.size.x-1.1f,floorBounds.size.z-1.1f);
            width=Mathf.FloorToInt(floor.width/Step)+1; depth=Mathf.FloorToInt(floor.height/Step)+1;
            obstacles.AddRange(rectangles);
            blocked=new bool[width*depth];
            for(int i=0;i<blocked.Length;i++) blocked[i]=!Clear(Point(i));
        }
        public bool Clear(Vector3 point)
        {
            var p=new Vector2(point.x,point.z);
            if(!floor.Contains(p)) return false;
            foreach(var obstacle in obstacles) if(obstacle.Contains(p)) return false;
            return true;
        }
        bool NavigationClear(Vector3 point)
        {
            if(!Clear(point)) return false;
            foreach(var body in avoided) {
                if((point-searchStart).sqrMagnitude<.0000001f)continue;
                float distance=(point-body).sqrMagnitude,initial=(searchStart-body).sqrMagnitude;
                if(distance<.7f*.7f && distance<=Mathf.Min(.7f*.7f,initial)+.000001f)return false;
            }
            return true;
        }
        Vector3 Point(int index)=>new Vector3(floor.xMin+(index%width)*Step,0,floor.yMin+(index/width)*Step);
        int Nearest(Vector3 point)
        {
            int best=-1; float distance=float.PositiveInfinity;
            for(int i=0;i<blocked.Length;i++)
            {
                if(blocked[i] || !NavigationClear(Point(i))) continue;
                float d=(Point(i)-point).sqrMagnitude;
                if(d<distance && Segment(point,Point(i))) { distance=d; best=i; }
            }
            return best;
        }
        bool Segment(Vector3 a,Vector3 b)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.1f);
            for(int n=0;n<=count;n++) if(!NavigationClear(Vector3.Lerp(a,b,count==0?0:(float)n/count))) return false;
            return true;
        }
        public List<Vector3> Find(Vector3 start,Vector3 destination,IEnumerable<Vector3> bodies=null)
        {
            start.y=destination.y=0;
            searchStart=start;avoided.Clear();
            if(bodies!=null) foreach(var body in bodies) avoided.Add(body);
            if(!NavigationClear(start) || !NavigationClear(destination)) return null;
            int from=Nearest(start),to=Nearest(destination);
            if(from<0 || to<0 || !Segment(start,Point(from)) || !Segment(Point(to),destination)) return null;
            var previous=new int[blocked.Length]; for(int i=0;i<previous.Length;i++) previous[i]=-2;
            var queue=new Queue<int>(); queue.Enqueue(from); previous[from]=-1;
            while(queue.Count>0 && previous[to]==-2)
            {
                int current=queue.Dequeue(),x=current%width,z=current/width;
                foreach(var delta in new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down})
                {
                    int nx=x+delta.x,nz=z+delta.y;
                    if(nx<0 || nx>=width || nz<0 || nz>=depth) continue;
                    int next=nz*width+nx;
                    if(blocked[next] || previous[next]!=-2 || !NavigationClear(Point(next)) || !Segment(Point(current),Point(next))) continue;
                    previous[next]=current; queue.Enqueue(next);
                }
            }
            if(previous[to]==-2) return null;
            var path=new List<Vector3>();
            for(int current=to;current>=0;current=previous[current]) path.Add(Point(current));
            path.Reverse(); path.Insert(0,start); path.Add(destination);
            // Keep only unobstructed shortcuts; this also removes unnecessary right-angle grid turns.
            var smooth=new List<Vector3>(); int anchor=0;
            while(anchor<path.Count-1)
            {
                int next=path.Count-1;
                while(next>anchor+1 && !Segment(path[anchor],path[next])) next--;
                smooth.Add(path[next]); anchor=next;
            }
            return smooth;
        }
    }
}
