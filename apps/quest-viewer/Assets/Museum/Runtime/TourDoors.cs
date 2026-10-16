using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed class TourDoors : MonoBehaviour
    {
        GameObject root;
        Material wall,frame,sign,landing;
        readonly Dictionary<string,Material> wallMaterials=new Dictionary<string,Material>();
        public void Build(MuseumRoom[] rooms,int current,Font font)
        {
            if(root!=null){root.SetActive(false);Destroy(root);}root=new GameObject("Tour doors and partitions");root.transform.SetParent(transform,false);
            if(wall==null){wall=new Material(Shader.Find("Standard"));frame=new Material(Shader.Find("Standard"));sign=new Material(Shader.Find("Standard"));}
            if(landing==null)landing=new Material(Shader.Find("Standard"));landing.color=RoomTheme.For(rooms[current]).Color("floor");
            wall.color=RoomTheme.For(rooms[current]).Color("backWall");frame.color=new Color(.55f,.38f,.16f);sign.color=new Color(.04f,.16f,.12f);
            var nav=TourNavigation.Get(rooms[current]);
            foreach(string side in new[]{"front","back","left","right"})
            {
                string name=side=="front"?"Front wall":side=="back"?"Back wall":side=="left"?"Left wall":"Right wall";
                var original=GameObject.Find(name);original.GetComponent<Renderer>().enabled=false;original.GetComponent<Collider>().enabled=false;
                bool horizontal=side=="front"||side=="back";float min=horizontal?-8.2f:-5f,max=horizontal?8.2f:14f;
                var cuts=new List<float>();foreach(var d in new[]{nav.entrance,nav.exit})if(d.side==side){var p=TourNavigation.Door(d);cuts.Add(horizontal?p.x:p.z);}cuts.Sort();
                float cursor=min;foreach(float c in cuts){WallPiece(side,cursor,c-.8f,0,4);WallPiece(side,c-.8f,c+.8f,2.7f,4);cursor=c+.8f;}WallPiece(side,cursor,max,0,4);
            }
            Landing(nav.entrance);Landing(nav.exit);
            Door(nav.entrance,current==0?"박물관 입구":"이전 방 · "+rooms[current-1].title,font);
            Door(nav.exit,current+1<rooms.Length?"다음 방 · "+rooms[current+1].title:"박물관 출구",font);
            foreach(int cell in nav.blockedCells){var rect=TourNavigation.Block(cell);Cube("통행 금지 벽",new Vector3(rect.center.x,1.25f,rect.center.y),new Vector3(rect.width,2.5f,rect.height),wall,true);}
        }
        void WallPiece(string side,float a,float b,float low,float high)
        {
            if(b<=a)return;bool horizontal=side=="front"||side=="back";
            if(!wallMaterials.TryGetValue(side,out var material)){material=new Material(Shader.Find("Standard"));wallMaterials[side]=material;}material.color=wall.color;
            material.color=GetComponent<GalleryDisplay>().CurrentTheme.Color(side=="front"?"frontWall":side=="back"?"backWall":side=="left"?"leftWall":"rightWall");
            Cube("Doorway wall segment",new Vector3(horizontal?(a+b)/2:side=="left"?-8.2f:8.2f,(low+high)/2,horizontal?(side=="front"?-5:14):(a+b)/2),horizontal?new Vector3(b-a,high-low,.2f):new Vector3(.2f,high-low,b-a),material,true);
        }
        void Landing(MuseumDoor d)
        {
            var holder=new GameObject("Safe exterior landing").transform;holder.SetParent(root.transform,false);holder.position=TourNavigation.Door(d)-TourNavigation.Inward(d)*2;holder.rotation=Quaternion.LookRotation(-TourNavigation.Inward(d));
            LocalCube(holder,"Exterior floor",new Vector3(0,-.1f,0),new Vector3(2.6f,.2f,3.2f),landing,true);
            LocalCube(holder,"Left safety rail",new Vector3(-1.3f,.7f,0),new Vector3(.18f,1.4f,3.3f),frame,true);
            LocalCube(holder,"Right safety rail",new Vector3(1.3f,.7f,0),new Vector3(.18f,1.4f,3.3f),frame,true);
            LocalCube(holder,"Outer safety rail",new Vector3(0,.7f,1.6f),new Vector3(2.8f,1.4f,.18f),frame,true);
        }
        void Door(MuseumDoor d,string title,Font font)
        {
            var holder=new GameObject(title).transform;holder.SetParent(root.transform,false);holder.position=TourNavigation.Door(d)-TourNavigation.Inward(d)*.8f;holder.rotation=Quaternion.LookRotation(-TourNavigation.Inward(d));
            LocalCube(holder,"Left jamb",new Vector3(-.83f,1.35f,0),new Vector3(.13f,2.7f,.22f),frame);
            LocalCube(holder,"Right jamb",new Vector3(.83f,1.35f,0),new Vector3(.13f,2.7f,.22f),frame);
            LocalCube(holder,"Door header",new Vector3(0,2.7f,0),new Vector3(1.8f,.15f,.22f),frame);
            LocalCube(holder,"Door label",new Vector3(0,3.04f,0),new Vector3(2.1f,.48f,.08f),sign);
            LocalCube(holder,"Threshold",new Vector3(0,.01f,-.55f),new Vector3(1.5f,.02f,1),frame);
            var label=new GameObject("Door destination").AddComponent<TextMesh>();label.transform.SetParent(holder,false);label.transform.localPosition=new Vector3(0,3.04f,-.06f);label.font=font;label.fontSize=64;label.characterSize=.025f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=Color.white;label.text=title;label.GetComponent<Renderer>().sharedMaterial=font.material;
            font.RequestCharactersInTexture(title,64);var bounds=label.GetComponent<Renderer>().localBounds;label.transform.localScale=Vector3.one*Mathf.Min(1,1.95f/Mathf.Max(.001f,bounds.size.x));
        }
        void LocalCube(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool collider=false){var cube=Cube(name,Vector3.zero,size,mat,collider);cube.transform.SetParent(parent,false);cube.transform.localPosition=p;}
        GameObject Cube(string name,Vector3 p,Vector3 size,Material mat,bool collider){var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name=name;cube.transform.SetParent(root.transform,false);cube.transform.position=p;cube.transform.localScale=size;cube.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Destroy(cube.GetComponent<Collider>());return cube;}
        void OnDestroy(){foreach(var m in wallMaterials.Values)Destroy(m);if(root!=null)Destroy(root);if(wall!=null)Destroy(wall);if(frame!=null)Destroy(frame);if(sign!=null)Destroy(sign);if(landing!=null)Destroy(landing);}
    }
}
