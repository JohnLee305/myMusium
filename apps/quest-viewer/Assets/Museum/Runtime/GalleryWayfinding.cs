using UnityEngine;

namespace FamilyMuseum
{
    // Wall-mounted signage shares materials; changing rooms replaces only its geometry.
    public sealed class GalleryWayfinding : MonoBehaviour
    {
        GameObject signs;
        Material enamel, brass, shadow;
        Font font;
        Color Ivory;
        Color Gold;

        void Awake()
        {
            enamel = Finish("Wayfinding / forest enamel", new Color(.035f,.095f,.08f), .15f);
            brass = Finish("Wayfinding / brushed brass", new Color(.57f,.38f,.16f), .65f);
            shadow = Finish("Wayfinding / shadow mount", new Color(.035f,.045f,.04f), 0);
        }
        static Material Finish(string name, Color color, float metal)
        {
            var result = new Material(Shader.Find("Standard")) { name = name, color = color };
            result.SetFloat("_Metallic", metal); result.SetFloat("_Glossiness", .3f);
            return result;
        }
        public void Build(float width, float back, Font labelFont, int current, MuseumRoom[] rooms)
        {
            var theme = RoomTheme.For(rooms[current]);
            enamel.color = theme.Color("signBackground"); brass.color = theme.Color("signBorder"); shadow.color = theme.Color("signMount");
            Ivory = theme.Color("signText"); Gold = theme.Color("signAccent");
            if (signs != null) { signs.SetActive(false); Destroy(signs); }
            signs = new GameObject("Wall-mounted gallery signage");
            signs.transform.SetParent(transform, false);
            font = labelFont;
            var nav=TourNavigation.Get(rooms[current]);bool backDoor=nav.entrance.side=="back"||nav.exit.side=="back";
            // Entrance/exit geometry and destination labels are built by TourDoors.

            var identity = new GameObject("Museum identity plaque").transform;
            identity.SetParent(signs.transform, false);
            identity.position = new Vector3(0,3.03f,back-.15f);
            identity.gameObject.SetActive(!backDoor);
            Block(identity, "Brass perimeter", Vector3.zero, new Vector3(5.9f,1.03f,.08f), brass);
            Block(identity, "Enamel face", new Vector3(0,0,-.046f), new Vector3(5.83f,.96f,.03f), enamel);
            foreach (string name in new[] { "Museum title", "Museum subtitle" })
            {
                var label = GameObject.Find(name).GetComponent<TextMesh>();
                var position = label.transform.position; position.z = back - .23f;
                label.transform.position = position; label.color = Ivory;label.GetComponent<Renderer>().enabled=!backDoor;
            }
            var accent = GameObject.Find("Wall accent").transform;
            accent.position = new Vector3(0,2.7f,back-.235f);accent.GetComponent<Renderer>().enabled=!backDoor;
        }
        void Plaque(int side, float width, int destination, int current, MuseumRoom[] rooms)
        {
            bool canTravel = destination >= 0 && destination < rooms.Length;
            int displayed = canTravel ? destination : current;
            var board = new GameObject(side < 0 ? "West entrance plaque" : "East entrance plaque").transform;
            board.SetParent(signs.transform, false);
            board.SetPositionAndRotation(new Vector3(side*(width/2-.15f),1.65f,-1.4f), Quaternion.Euler(0,side*90,0));
            Block(board,"Mount",new Vector3(0,0,.01f),new Vector3(1.82f,2.3f,.08f),shadow);
            Block(board,"Brass frame",new Vector3(0,0,-.04f),new Vector3(1.76f,2.24f,.06f),brass);
            Block(board,"Recessed green face",new Vector3(0,0,-.079f),new Vector3(1.70f,2.18f,.026f),enamel);
            Block(board,"Divider",new Vector3(0,-.30f,-.095f),new Vector3(1.34f,.008f,.008f),brass);
            Text(board,"Collection marker","F A M I L Y   A R C H I V E",.87f,.72f,1.4f,.10f,Gold);
            Text(board,"Room number",(displayed+1).ToString("00"),.46f,3.2f,1.4f,.45f,Ivory);
            string title = rooms[displayed].title;
            if (title.Length > 28) title = title.Substring(0,27) + "…";
            if (title.Length > 14) title = title.Insert(14,"\n");
            Text(board,"Destination",title,-.05f,1.2f,1.4f,.32f,Ivory);
            Text(board,"Direction",canTravel ? (side < 0 ? "←  이전 전시실" : "다음 전시실  →") : "현재 전시실",-.52f,1f,1.4f,.20f,Gold);
            Text(board,"Instruction",canTravel ? "바닥 표시 안으로 이동하세요" : "천천히, 기억을 둘러보세요",-.78f,.75f,1.4f,.12f,Ivory);
            Text(board,"Exhibit count",$"{rooms[displayed].artifacts.Length:D2} OBJECTS",-.96f,.6f,1.4f,.08f,Gold);
            if (!canTravel) return;
            // Mark the existing approach trigger on the floor without obstructing the aisle.
            var marker = new GameObject("Entrance floor inlay").transform;
            marker.SetParent(signs.transform,false);
            marker.position = new Vector3(side*(width/2-1.2f),.008f,-1.4f);
            Block(marker,"Front inlay",new Vector3(0,0,-.68f),new Vector3(1.1f,.008f,.026f),brass);
            Block(marker,"Back inlay",new Vector3(0,0,.68f),new Vector3(1.1f,.008f,.026f),brass);
            Block(marker,"Left inlay",new Vector3(-.55f,0,0),new Vector3(.026f,.008f,1.36f),brass);
            Block(marker,"Right inlay",new Vector3(.55f,0,0),new Vector3(.026f,.008f,1.36f),brass);
        }
        void Block(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name=name; item.transform.SetParent(parent,false);
            item.transform.localPosition=position; item.transform.localScale=size;
            item.GetComponent<Renderer>().sharedMaterial=material;
            Destroy(item.GetComponent<Collider>());
        }
        void Text(Transform parent,string name,string value,float y,float size,float width,float height,Color color)
        {
            var label=new GameObject(name).AddComponent<TextMesh>();
            label.transform.SetParent(parent,false); label.transform.localPosition=new Vector3(0,y,-.10f);
            label.font=font; label.fontSize=64; label.characterSize=.025f*size;
            label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center;
            label.color=color; label.text=value;
            font.RequestCharactersInTexture(value,64);
            label.GetComponent<Renderer>().sharedMaterial=font.material;
            // Side plaques face +/-90 degrees: world Z is the text's horizontal axis.
            var bounds=label.GetComponent<Renderer>().bounds;
            float fit=Mathf.Min(1,width/Mathf.Max(bounds.size.z,.001f),height/Mathf.Max(bounds.size.y,.001f));
            label.transform.localScale=Vector3.one*fit;
        }
        void OnDestroy()
        {
            if(signs!=null) Destroy(signs);
            if(enamel!=null) Destroy(enamel);
            if(brass!=null) Destroy(brass);
            if(shadow!=null) Destroy(shadow);
        }
    }
}
