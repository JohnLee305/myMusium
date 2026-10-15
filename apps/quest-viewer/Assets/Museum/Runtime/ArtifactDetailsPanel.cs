using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FamilyMuseum
{
    // Desktop reading UI. Headset interaction is a separate, unverified feature.
    [DefaultExecutionOrder(-50)]
    public sealed class ArtifactDetailsPanel : MonoBehaviour
    {
        public bool IsOpen => selected != null;
        public bool BlocksNavigation => IsOpen || Time.frameCount == closedFrame;
        public bool SmokePassed { get; private set; }
        ArtifactDisplay selected;
        GalleryDisplay gallery;
        MuseumSession session;
        Font font;
        ArtifactInspection inspection;
        string inspectionError;
        Vector2 scroll;
        int closedFrame = -1;

        void Awake()
        {
            gallery = GetComponent<GalleryDisplay>();
            session = GetComponent<MuseumSession>();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            inspection = gameObject.AddComponent<ArtifactInspection>();
        }
        void Update()
        {
            if (!session.desktopRig.activeInHierarchy || gallery.IsLoading) { Close(); return; }
            var keys = Keyboard.current;
            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (keys != null && keys.escapeKey.wasPressedThisFrame) Close();
                else if (keys != null && keys.rKey.wasPressedThisFrame && inspection.Active) inspection.ResetView();
                return;
            }
            if (!gallery.IsFinished || BlocksNavigation || Camera.main == null) return;
            if (keys != null && keys.fKey.wasPressedThisFrame)
                SelectRay(Camera.main.ViewportPointToRay(new Vector3(.5f, .5f)));
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || mouse.rightButton.isPressed) return;
            Vector2 point = mouse.position.ReadValue();
            // Existing gallery and help overlays own these screen areas.
            if (point.y > Screen.height - 110 || point.y < 130) return;
            if (GetComponent<MuseumVisitors>()?.BlocksPointer(point) ?? false) return;
            SelectRay(Camera.main.ScreenPointToRay(point));
        }
        public bool SelectRay(Ray ray)
        {
            if (gallery.IsLoading || !gallery.IsFinished || !session.desktopRig.activeInHierarchy) return false;
            if (!Physics.Raycast(ray, out var hit, 12f, ~0, QueryTriggerInteraction.Collide)) return false;
            var slot = hit.collider.GetComponentInParent<ArtifactDisplay>();
            if (slot == null || !slot.managedSlot || !slot.IsFinished) return false;
            selected = slot;
            inspection.Close(); inspectionError = null;
            scroll = Vector2.zero;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("DETAILS_OPEN: " + (slot.Data?.id ?? "unavailable"));
            return true;
        }
        public void Close()
        {
            inspection?.Close();
            if (!IsOpen) return;
            selected = null;
            scroll = Vector2.zero;
            closedFrame = Time.frameCount;
            Debug.Log("DETAILS_CLOSED");
        }
        void OnGUI()
        {
            if (!session.desktopRig.activeInHierarchy || gallery.IsLoading) return;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float scale = Mathf.Min(1.5f, Screen.width / 960f, Screen.height / 640f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            var body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, wordWrap = true, richText = false };
            body.normal.textColor = IsOpen ? gallery.CurrentTheme.Color("detailText") : new Color(.9f, .91f, .86f);
            if (!IsOpen)
            {
                body.fontSize = 16; body.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(0, height - 32, width, 28), "전시품·전시대 클릭 또는 화면 중앙에 맞춘 뒤 F · 가까이에서 설명 읽기", body);
                GUI.Label(new Rect(width / 2 - 10, height / 2 - 10, 20, 20), "+", body);
            }
            else
            {
                GUI.depth = -100;
                GUI.color = new Color(0, 0, 0, .72f);
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                float panelWidth = inspection.Active ? 920 : 760;
                var panel = new Rect((width - panelWidth) / 2, (height - 520) / 2, panelWidth, 520);
                GUI.color = gallery.CurrentTheme.Color("detailBackground");
                GUI.DrawTexture(panel, Texture2D.whiteTexture);
                GUI.color = gallery.CurrentTheme.Color("detailAccent");
                GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 4), Texture2D.whiteTexture);
                GUI.color = oldColor;
                var small = new GUIStyle(body) { fontSize = 16 };
                GUI.Label(new Rect(panel.x + 32, panel.y + 22, 500, 28), "전시품 이야기", small);
                var button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18 };
                if (GUI.Button(new Rect(panel.xMax - 142, panel.y + 18, 110, 38), "닫기 · Esc", button)) Close();
                if (IsOpen)
                {
                    string title = selected.Data?.title ?? "전시품을 불러올 수 없습니다";
                    string description = selected.Data?.description ?? "전시 자료를 읽지 못했습니다. 관리 화면에서 자료를 확인해 주세요.";
                    if (!selected.Loaded) description = "※ 3D 모델을 표시할 수 없습니다.\n\n" + description;
                    var heading = new GUIStyle(body) { fontSize = 30, fontStyle = FontStyle.Bold };
                    var viewport = inspection.Active ? new Rect(panel.x+506,panel.y+76,382,374) : new Rect(panel.x + 32, panel.y + 76, 696, 374);
                    if (inspection.Active)
                    {
                        inspection.Draw(new Rect(panel.x+32,panel.y+72,442,348));
                        float x = panel.x+32, y=panel.y+426;
                        if (GUI.Button(new Rect(x,y,70,34),"↶",button)) inspection.Rotate(new Vector2(-30,0));
                        if (GUI.Button(new Rect(x+76,y,70,34),"↷",button)) inspection.Rotate(new Vector2(30,0));
                        if (GUI.Button(new Rect(x+152,y,60,34),"−",button)) inspection.SetZoom(inspection.Zoom*.85f);
                        if (GUI.Button(new Rect(x+218,y,60,34),"+",button)) inspection.SetZoom(inspection.Zoom/ .85f);
                        if (GUI.Button(new Rect(x+284,y,150,34),"초기화 · R",button)) inspection.ResetView();
                    }
                    float contentWidth = viewport.width - 28;
                    float titleHeight = heading.CalcHeight(new GUIContent(title), contentWidth);
                    float textHeight = body.CalcHeight(new GUIContent(description), contentWidth);
                    scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, contentWidth, titleHeight + textHeight + 30));
                    GUI.Label(new Rect(0, 0, contentWidth, titleHeight), title, heading);
                    GUI.Label(new Rect(0, titleHeight + 24, contentWidth, textHeight), description, body);
                    GUI.EndScrollView();
                    GUI.Label(new Rect(panel.x + 32, panel.yMax - 46, panel.width-230, 28), inspection.Active ? "모델 드래그: 회전 · 모델 위 휠: 확대/축소" : (inspectionError ?? "마우스 휠로 읽기 · 닫으면 관람을 계속합니다"), small);
                    GUI.enabled = selected.Loaded;
                    if (GUI.Button(new Rect(panel.xMax-190,panel.yMax-50,158,36),inspection.Active ? "설명만 보기" : "3D 크게 보기",button))
                    {
                        if (inspection.Active) inspection.Close();
                        else if (!inspection.Open(selected)) inspectionError="모델을 확대할 수 없습니다.";
                        scroll=Vector2.zero;
                    }
                    GUI.enabled = true;
                }
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
        public IEnumerator RunSmoke()
        {
            SmokePassed = true;
            Physics.SyncTransforms();
            var slots = FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None).Where(s => s.managedSlot).ToArray();
            SmokePassed &= slots.Length > 0;
            foreach (var slot in slots.Take(3))
            {
                Vector3 target = slot.modelSlot.position; target.y = .5f;
                SmokePassed &= SelectRay(new Ray(Camera.main.transform.position, target - Camera.main.transform.position)) && selected == slot && BlocksNavigation;
                Close();
                SmokePassed &= !IsOpen;
                yield return null;
                SmokePassed &= !BlocksNavigation;
            }
            SmokePassed &= !SelectRay(new Ray(Camera.main.transform.position, Vector3.up));
            if (slots.Length > 0)
            {
                Vector3 target = slots[0].modelSlot.position; target.y = .5f;
                SelectRay(new Ray(Camera.main.transform.position, target - Camera.main.transform.position));
                if (gallery.RoomCount > 1)
                {
                    var task = gallery.SwitchRoomAsync(1);
                    SmokePassed &= !IsOpen;
                    while (!task.IsCompleted) yield return null;
                    SmokePassed &= !task.IsFaulted && gallery.CurrentRoom == 1;
                    task = gallery.SwitchRoomAsync(0);
                    while (!task.IsCompleted) yield return null;
                    SmokePassed &= !task.IsFaulted && gallery.CurrentRoom == 0;
                    Physics.SyncTransforms();
                    SelectRay(new Ray(Camera.main.transform.position, target - Camera.main.transform.position));
                }
                SmokePassed &= IsOpen;
            }
            Debug.Log(SmokePassed ? "DETAILS_SMOKE_PASS" : "DETAILS_SMOKE_FAIL");
        }
        public IEnumerator RunInspectionSmoke()
        {
            SmokePassed = true;
            var slots=FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None).Where(s=>s.managedSlot).ToArray();
            SmokePassed &= slots.Length>0;
            foreach(var slot in slots)
            {
                selected=slot;
                var position=slot.modelSlot.position; var rotation=slot.modelSlot.rotation; var scale=slot.modelSlot.localScale;
                int imports=ArtifactDisplay.ActiveImports;
                bool opened=inspection.Open(slot); SmokePassed &= opened==slot.Loaded;
                if(opened)
                {
                    inspection.Rotate(new Vector2(190,50)); inspection.SetZoom(100); SmokePassed &= inspection.Zoom==2.5f;
                    inspection.SetZoom(0); SmokePassed &= inspection.Zoom==.6f;
                    inspection.ResetView(); SmokePassed &= inspection.Zoom==1 && inspection.Angles==new Vector2(25,-15);
                    SmokePassed &= position==slot.modelSlot.position && rotation==slot.modelSlot.rotation && scale==slot.modelSlot.localScale && imports==ArtifactDisplay.ActiveImports && BlocksNavigation;
                }
                Close(); SmokePassed &= !inspection.Active && inspection.Texture==null && !IsOpen;
                yield return null;
                SmokePassed &= GameObject.Find("Inspection stage")==null && ArtifactDisplay.ActiveImports==imports;
            }
            if(gallery.RoomCount>1 && slots.Length>0)
            {
                selected=slots[0]; inspection.Open(selected);
                var task=gallery.SwitchRoomAsync(1); SmokePassed &= !inspection.Active && !IsOpen;
                while(!task.IsCompleted)yield return null;
                task=gallery.SwitchRoomAsync(0); while(!task.IsCompleted)yield return null;
                SmokePassed &= !task.IsFaulted;
            }
            var first=FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None).FirstOrDefault(s=>s.managedSlot);
            if(first!=null){selected=first;inspection.Open(first);}
            Debug.Log(SmokePassed ? "INSPECTION_SMOKE_PASS" : "INSPECTION_SMOKE_FAIL");
        }
        void OnDestroy() { inspection?.Close(); if (font != null) Destroy(font); }
    }
}
