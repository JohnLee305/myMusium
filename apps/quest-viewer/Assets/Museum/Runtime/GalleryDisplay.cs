using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FamilyMuseum
{
    public sealed class GalleryDisplay : MonoBehaviour
    {
        public const int Capacity = 30;
        public bool IsFinished { get; private set; }
        public bool IsLoading { get; private set; }
        public int LoadedCount { get; private set; }
        public int FailedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int CurrentRoom { get; private set; } = -1;
        public int RoomCount => rooms.Length;
        public int ExpectedCount => CurrentRoom >= 0 ? rooms[CurrentRoom].artifacts.Length : 0;
        public string StateLabel { get; private set; } = "Loading gallery...";
        public RoomTheme CurrentTheme => CurrentRoom >= 0 ? RoomTheme.For(rooms[CurrentRoom]) : RoomTheme.Default;
        RoomPalette environmentPalette;
        public string RoomTitle => CurrentRoom >= 0 ? rooms[CurrentRoom].title : "Museum";
        MuseumRoom[] rooms = Array.Empty<MuseumRoom>();
        LocalArchive archive;
        ArtifactDisplay template;
        GameObject plinth, cap, activeRoot;
        Font font;
        Vector3 leftPortal, rightPortal;
        GalleryWayfinding wayfinding;
        TourDoors tourDoors;
        float portalReadyAt;

        public async Task LoadAsync(ArtifactDisplay first)
        {
            template = first;
            if (GetComponent<ArtifactDetailsPanel>() == null) gameObject.AddComponent<ArtifactDetailsPanel>();
            plinth = GameObject.Find("Display plinth"); cap = GameObject.Find("Display cap");
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 64);
            font.material.shader = Resources.Load<Shader>("WorldText");
            foreach (var obj in new[] { first.modelSlot.gameObject, first.placeholder, first.titleLabel.gameObject, first.descriptionLabel.gameObject, plinth, cap }) obj.SetActive(false);
            try
            {
                archive = new LocalArchive(LocalArchive.FindRoot());
                rooms = archive.ReadRooms();
                await SwitchRoomAsync(0);
            }
            catch (Exception error)
            {
                if (destroyCancellationToken.IsCancellationRequested) return;
                foreach (var obj in new[] { first.placeholder, first.titleLabel.gameObject, first.descriptionLabel.gameObject, plinth, cap }) obj.SetActive(true);
                await first.LoadAsync(null, "manifest.json", error.Message);
                FailedCount = 1; IsFinished = true;
                StateLabel = "전시 목록을 확인해 주세요.";
                Debug.Log("GALLERY_READY: loaded=0 failed=1 skipped=0");
            }
        }

        public async Task SwitchRoomAsync(int index)
        {
            if (IsLoading || index < 0 || index >= rooms.Length || index == CurrentRoom) return;
            GetComponent<ArtifactDetailsPanel>()?.Close();
            GetComponent<VRExhibitInteraction>()?.Cancel();
            // Guests retain their logical tour while only the displayed room is loaded.
            var visitors=GetComponent<MuseumVisitors>();if(visitors!=null && !visitors.OrderedTours)visitors.ClearRoom();
            IsLoading = true; IsFinished = false;
            var navigator = GetComponent<MuseumSession>().desktopRig.GetComponent<DesktopNavigator>();
            navigator.enabled = false;
            if (activeRoot != null) activeRoot.SetActive(false);
            GameObject pending = new GameObject("Loading room " + rooms[index].id);
            var palette = pending.AddComponent<RoomPalette>();
            var theme = RoomTheme.For(rooms[index]);
            int loaded = 0, failed = 0;
            try
            {
                var entries = rooms[index].artifacts;
                int columns = 6;
                for (int i = 0; i < entries.Length; i++)
                {
                    destroyCancellationToken.ThrowIfCancellationRequested();
                    StateLabel = $"{rooms[index].title} 불러오는 중 · {i + 1}/{entries.Length}";
                    var root = new GameObject("Exhibit slot " + (i + 1));
                    root.transform.SetParent(pending.transform, false);
                    var slot = root.AddComponent<ArtifactDisplay>(); slot.managedSlot = true; slot.Theme = theme;
                    slot.modelSlot = Clone(template.modelSlot.gameObject, root.transform).transform;
                    slot.placeholder = Clone(template.placeholder, root.transform);
                    slot.titleLabel = Clone(template.titleLabel.gameObject, root.transform).GetComponent<TextMesh>();
                    slot.descriptionLabel = Clone(template.descriptionLabel.gameObject, root.transform).GetComponent<TextMesh>();
                    palette.Paint(Clone(plinth, root.transform), theme.Color("plinth"));
                    palette.Paint(Clone(cap, root.transform), theme.Color("cap"));
                    int cell = rooms[index].cells[i];
                    root.transform.localPosition = new Vector3((cell % columns - 2.5f) * 2.4f, 0, cell / columns * 2.8f);
                    Debug.Log($"GRID_POSITION: entry={entries[i]} cell={cell} x={root.transform.localPosition.x} z={root.transform.localPosition.z}");
                    await slot.LoadAsync(archive, entries[i]);
                    if (slot.Loaded) loaded++; else failed++;
                }
                destroyCancellationToken.ThrowIfCancellationRequested();
                var previous = activeRoot;
                activeRoot = pending; pending = null;
                if (previous != null)
                {
                    previous.SetActive(false);
                    Destroy(previous);
                    // OnDestroy disposes every room's glTF importer and font before readiness is reported.
                    await Task.Yield();
                    Debug.Log("ROOM_RELEASED: " + rooms[CurrentRoom].id);
                }
                CurrentRoom = index; LoadedCount = loaded; FailedCount = failed; SkippedCount = 0;
                ResizeRoom(entries.Length);
                ResetVisitor();
                var cleanup = Resources.UnloadUnusedAssets();
                while (!cleanup.isDone) { destroyCancellationToken.ThrowIfCancellationRequested(); await Task.Yield(); }
                StateLabel = $"{RoomTitle} · {loaded}개 전시 / {failed}개 확인 필요";
                IsFinished = true;
                Debug.Log($"ROOM_READY: room={index + 1}/{rooms.Length} loaded={loaded} failed={failed} activeSlots={activeRoot.GetComponentsInChildren<ArtifactDisplay>().Length}");
                Debug.Log($"GALLERY_READY: loaded={loaded} failed={failed} skipped=0");
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                StateLabel = "방을 불러오지 못했습니다. 다시 이동해 주세요.";
                Debug.LogWarning("ROOM_FAILED: " + error.Message);
                if (CurrentRoom < 0) { FailedCount = 1; IsFinished = true; }
                else IsFinished = true;
            }
            finally
            {
                if (pending != null) Destroy(pending);
                if (activeRoot != null) activeRoot.SetActive(true);
                IsLoading = false;
                if (!destroyCancellationToken.IsCancellationRequested) navigator.enabled = true;
                portalReadyAt = Time.unscaledTime + 2f;
            }
        }
        static GameObject Clone(GameObject original, Transform parent)
        {
            var copy = Instantiate(original, parent); copy.SetActive(true); return copy;
        }
        void Box(string name, Vector3 position, Vector3 size)
        {
            var obj = GameObject.Find(name); obj.transform.position = position; obj.transform.localScale = size;
        }
        void ResizeRoom(int count)
        {
            int columns = 6, rows = 5;
            float width = Math.Max(10f, columns * 2.4f + 2f), back = Math.Max(4f, .8f + (rows - 1) * 2.8f + 2f);
            Box("Floor", new Vector3(0,-.1f,(back-5)/2), new Vector3(width,.2f,back+5));
            Box("Back wall", new Vector3(0,2,back), new Vector3(width,4,.2f));
            Box("Front wall", new Vector3(0,2,-5), new Vector3(width,4,.2f));
            Box("Left wall", new Vector3(-width/2,2,(back-5)/2), new Vector3(.2f,4,back+5));
            Box("Right wall", new Vector3(width/2,2,(back-5)/2), new Vector3(.2f,4,back+5));
            foreach (string name in new[] { "Museum title", "Museum subtitle", "Wall accent" })
            {
                var item = GameObject.Find(name).transform; var pos = item.position; pos.z = back - .15f; item.position = pos;
            }
            leftPortal=TourNavigation.Door(TourNavigation.Get(rooms[CurrentRoom]).entrance);
            rightPortal=TourNavigation.Door(TourNavigation.Get(rooms[CurrentRoom]).exit);
            if (wayfinding == null) wayfinding = gameObject.AddComponent<GalleryWayfinding>();
            if (environmentPalette == null) environmentPalette = gameObject.AddComponent<RoomPalette>();
            environmentPalette.ApplyRoom(CurrentTheme);
            wayfinding.Build(width, back, font, CurrentRoom, rooms);
            if(tourDoors==null)tourDoors=gameObject.AddComponent<TourDoors>();tourDoors.Build(rooms,CurrentRoom,font);
        }
        public void PreviewWayfinding()
        {
            var rig = GetComponent<MuseumSession>().desktopRig;
            var controller = rig.GetComponent<CharacterController>();
            controller.enabled = false;
            var inward=TourNavigation.Inward(TourNavigation.Get(rooms[CurrentRoom]).exit);
            rig.transform.SetPositionAndRotation(rightPortal+inward*2.4f+Vector3.up*.05f,Quaternion.LookRotation(-inward));
            controller.enabled = true;
        }
        void ResetVisitor()
        {
            var session = GetComponent<MuseumSession>();
            var controller = session.desktopRig.GetComponent<CharacterController>();
            controller.enabled = false;
            var nav=TourNavigation.Get(rooms[CurrentRoom]);var paths=TourNavigation.Paths(rooms[CurrentRoom]);var entry=TourNavigation.Door(nav.entrance);var forward=TourNavigation.Inward(nav.entrance);var side=Vector3.Cross(Vector3.up,forward);var spawn=entry;
            foreach(var candidate in new[]{entry+forward*2+side*.9f,entry+forward*2-side*.9f,entry+forward*1.2f,entry})if(paths.Clear(candidate)&&paths.Find(entry,candidate)!=null){spawn=candidate;break;}
            session.desktopRig.transform.SetPositionAndRotation(spawn+Vector3.up*.05f, Quaternion.LookRotation(forward));
            controller.enabled = true;
            var xr = session.xrRig.transform;
            var head = session.xrRig.GetComponentInChildren<Camera>(true).transform;
            var offset = head.position - xr.position;
            xr.position = new Vector3(spawn.x-offset.x, 0, spawn.z-offset.z);
        }
        void Update()
        {
            if (!IsFinished || IsLoading || RoomCount < 2 || GetComponent<ArtifactDetailsPanel>().BlocksNavigation || (GetComponent<VRExhibitInteraction>()?.Busy ?? false)) return;
            var keys = Keyboard.current;
            if (keys != null && keys.pageDownKey.wasPressedThisFrame) { _ = SwitchRoomAsync(CurrentRoom + 1); return; }
            if (keys != null && keys.pageUpKey.wasPressedThisFrame) { _ = SwitchRoomAsync(CurrentRoom - 1); return; }
            if (Time.unscaledTime < portalReadyAt || Camera.main == null) return;
            Vector3 pos = Camera.main.transform.position; pos.y = 0;
            if (Vector3.Distance(pos, leftPortal) < .8f) _ = SwitchRoomAsync(CurrentRoom - 1);
            else if (Vector3.Distance(pos, rightPortal) < .8f) _ = SwitchRoomAsync(CurrentRoom + 1);
        }
        void OnGUI()
        {
            if (GetComponent<ArtifactDetailsPanel>().IsOpen) return;
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, alignment = TextAnchor.MiddleCenter };
            if (IsLoading)
            {
                var color = GUI.color; GUI.color = new Color(.07f,.11f,.1f,1);
                GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height), Texture2D.whiteTexture);
                GUI.color = color;
                GUI.Label(new Rect(0,Screen.height/2-30,Screen.width,60), StateLabel, style);
                return;
            }
            GUI.Box(new Rect(20,20,Math.Min(750,Screen.width-40),85), "");
            GUI.Label(new Rect(120,28,480,30), $"{RoomTitle} · {CurrentRoom+1}/{RoomCount} · {ExpectedCount}/30", style);
            GUI.enabled = CurrentRoom > 0;
            if (GUI.Button(new Rect(30,37,85,35), "< Previous")) _ = SwitchRoomAsync(CurrentRoom-1);
            GUI.enabled = CurrentRoom+1 < RoomCount;
            if (GUI.Button(new Rect(620,37,100,35), "Next >")) _ = SwitchRoomAsync(CurrentRoom+1);
            GUI.enabled = true;
            GUI.Label(new Rect(120,61,480,30), "Page Up / Page Down · 출입문에 가까이 가도 이동합니다", style);
        }
        void OnDestroy()
        {
            if (activeRoot != null) Destroy(activeRoot);
            if (font != null) Destroy(font);
        }
    }
}


