using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace FamilyMuseum.Decor
{
    public sealed class DecorSelectable : MonoBehaviour { public string InstanceId; }

    [RequireComponent(typeof(UIDocument))]
    public sealed class DecorPanel : MonoBehaviour
    {
        public UnityEngine.TextAsset fixture;
        public StyleSheet styleSheet;
        public DecorCamera cameraRig;
        public readonly EditorPresentationSettings Settings = new EditorPresentationSettings();
        public DecorDocument Document { get; private set; }
        public string SelectedInstanceId { get; private set; }
        public VisualElement Root { get; private set; }
        public bool Ready { get; private set; }
        public DecorEditorController Editor { get; private set; }
        public DecorPrototypeSession Session { get; private set; }
        public event Action<ItemInstance> SelectionChanged;
        public event Action<ItemDefinition> CatalogSelected;
        readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
        readonly List<GameObject> walls = new List<GameObject>();
        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<string, Material> sharedMaterials = new Dictionary<string, Material>();
        readonly List<GameObject> outlines = new List<GameObject>();
        readonly Dictionary<Color, Material> outlineMaterials = new Dictionary<Color, Material>();
        GameObject worldRoot, gridRoot, itemRoot; FontAsset fontAsset;
        TextField search; ScrollView cards;
        bool inventory, compact, detailsOpen, gridVisible = true; int wallMode = 1; string filter = "";
        public bool TextEditing
        {
            get
            {
                for (var e = Root?.focusController?.focusedElement as VisualElement; e != null; e = e.parent)
                    if (e is TextField) return true;
                return false;
            }
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            if (!DecorJson.TryRead(fixture.text, out var document, out var format)) throw new InvalidOperationException("Prototype fixture format invalid");
            var placement = DecorPlacementValidator.Validate(document.catalog, document.world);
            if (!placement.ok) throw new InvalidOperationException("Prototype fixture placement invalid: " + string.Join(",", placement.issues.Select(i => i.code)));
            Document = document;
            Session = gameObject.AddComponent<DecorPrototypeSession>(); Session.Open(this);
            var documentRoot = GetComponent<UIDocument>().rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore; documentRoot.styleSheets.Add(styleSheet);
            Root = documentRoot.Q("decor-root");
            if (Root == null) throw new InvalidOperationException("Decor UI template missing");
            Root.pickingMode = PickingMode.Ignore; Root.Q("viewport").pickingMode = PickingMode.Ignore; Root.Q("room-caption").pickingMode = PickingMode.Ignore;
            var sourceFont = Resources.Load<Font>(Settings.fontResource);
            fontAsset = FontAsset.CreateFontAsset(sourceFont); fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Root.style.unityFontDefinition = FontDefinition.FromSDFFont(fontAsset);
            Root.style.fontSize = Settings.bodySize; Root.style.color = ColorOf(Settings.ink);
            foreach (var surface in Root.Query(className: "surface").ToList()) surface.style.backgroundColor = ColorOf(Settings.background);
            foreach (var caption in Root.Query(className: "caption").ToList()) { caption.style.fontSize = Settings.captionSize; caption.style.color = ColorOf(Settings.muted); }
            Root.Q("toolbar").style.minHeight = Settings.toolbarHeight;
            Root.Q("details").style.width = Settings.detailsWidth;
            Root.Q("catalog").style.minHeight = Settings.catalogHeight;
            Root.Q("parent").style.display = DisplayStyle.None;
            search = Root.Q<TextField>("search"); cards = Root.Q<ScrollView>("cards");
            search.labelElement.style.minWidth = 0; search.labelElement.style.width = 44;
            search.RegisterValueChangedCallback(_ => RefreshCards());
            Bind("catalog-tab", () => { inventory = false; RefreshCards(); });
            Bind("inventory-tab", () => { inventory = true; RefreshCards(); });
            Bind("all-filter", () => { filter = ""; RefreshCards(); });
            Bind("furniture-filter", () => { filter = "floorFurniture"; RefreshCards(); });
            Bind("decor-filter", () => { filter = "surfaceDecor"; RefreshCards(); });
            Bind("room-frame", FrameRoom); Bind("focus", FrameSelection); Bind("walls", CycleWalls); Bind("grid-toggle", ToggleGrid);
            Bind("details-toggle", () => { detailsOpen = !detailsOpen; Layout(); });
            Bind("help", () => { var e = Root.Q("help-text"); e.style.display = e.resolvedStyle.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None; });
            Bind("parent", () => { var item = Document.world.instances.FirstOrDefault(i => i.id == SelectedInstanceId); if (item?.location.kind == "parent") SelectInstance(item.location.parentId); });
            foreach (var id in new[] { "collection-tab", "space-tab" })
            { var b = Root.Q<Button>(id); b.SetEnabled(false); b.tooltip = "실제 소장품 연결과 공간·마감 편집은 후속 단계입니다."; }
            Root.Q<Label>("list-hint").text = "카드 선택 후 클릭 · 적용하면 시험 배치를 저장합니다";
            Root.RegisterCallback<GeometryChangedEvent>(_ => Layout());
            if (FindFirstObjectByType<EventSystem>() == null)
            { var events = new GameObject("Decor UI Events"); events.transform.SetParent(transform); events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>(); }
            BuildWorld(); cameraRig.Initialize(cameraRig.GetComponent<Camera>(), Settings);
            Editor = gameObject.AddComponent<DecorEditorController>(); Editor.Initialize(this);
            Bind("move-item", Editor.MoveSelected); Bind("rotate-left", () => Editor.Rotate(-1)); Bind("rotate-right", () => Editor.Rotate(1));
            Bind("store-item", Editor.Store); Bind("undo", Editor.Undo); Bind("redo", Editor.Redo); Bind("cancel-preview", Editor.Cancel);
            gameObject.AddComponent<DecorInputRouter>().Initialize(this, cameraRig);
            RefreshCards(); RefreshTools(); SetStatus("꾸미기 시험실 · 카탈로그에서 물건을 골라 놓아 보세요. 이번 실행의 변경은 저장되지 않습니다.");
            Session.BindUI();
            yield return null; Layout(); Ready = true;
            string[] args = Environment.GetCommandLineArgs(); int smoke = Array.IndexOf(args, "-decor-smoke");
            if (smoke >= 0 && smoke + 1 < args.Length) gameObject.AddComponent<DecorPrototypeSmoke>().Begin(this, args[smoke + 1]);
            int probe = Array.IndexOf(args, "-decor-probe");
            if (probe >= 0 && probe + 2 < args.Length) gameObject.AddComponent<DecorRestartProbe>().Begin(this, args[probe + 2]);
        }
        void Bind(string id, Action action) { Root.Q<Button>(id).clicked += action; }
        public void SetStatus(string message) { Root.Q<Label>("status").text = message; }
        public bool IsOverUI(Vector2 screen)
        {
            if (Root?.panel == null) return true;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = Root.panel.Pick(point);
            return picked != null && picked != GetComponent<UIDocument>().rootVisualElement;
        }
        void Layout()
        {
            if (Root == null || cameraRig.View == null || float.IsNaN(Root.resolvedStyle.width) || float.IsInfinity(Root.resolvedStyle.width) || Root.resolvedStyle.width < 1) return;
            compact = Root.resolvedStyle.width < Settings.compactWidth;
            bool shortWindow = Root.resolvedStyle.height < 600;
            Root.Q("viewport").style.minHeight = shortWindow ? 64 : 120;
            cards.style.minHeight = 0; cards.style.height = shortWindow ? 112 : 150;
            Root.Q("catalog").style.minHeight = shortWindow ? Settings.shortCatalogHeight : Settings.catalogHeight;
            Root.Q("catalog").style.height = shortWindow ? Settings.shortCatalogHeight : Settings.catalogHeight;
            foreach (var button in Root.Q("toolbar").Query<Button>().ToList())
            { button.style.height = shortWindow ? 30 : 34; button.style.minHeight = shortWindow ? 30 : 34; button.style.paddingTop = button.style.paddingBottom = 0; }
            foreach (var card in Root.Query(className: "card").ToList())
            { card.style.height = shortWindow ? 84 : Settings.cardHeight; card.style.paddingTop = card.style.paddingBottom = shortWindow ? 5 : 10; }
            foreach (var name in Root.Query(className: "card-name").ToList()) name.style.height = shortWindow ? 40 : 44;
            foreach (var thumbnail in Root.Query(className: "card-thumbnail").ToList()) thumbnail.style.display = shortWindow ? DisplayStyle.None : DisplayStyle.Flex;
            Root.Q("list-hint").style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            Root.Q("details").style.display = !compact || detailsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            Root.Q("details-toggle").style.display = compact ? DisplayStyle.Flex : DisplayStyle.None;
            var rect = Root.Q("viewport").worldBound; float w = Root.worldBound.width, h = Root.worldBound.height;
            if (rect.height > 0 && w > 0 && h > 0) cameraRig.View.rect = new Rect(rect.x / w, 1 - rect.yMax / h, rect.width / w, rect.height / h);
        }
        public void FrameRoom() { cameraRig.Frame(new Bounds(new Vector3(0, .5f, 0), new Vector3(20, 1, 16))); SetStatus("방 전체 보기 · F로 다시 맞출 수 있습니다."); }
        public void FrameSelection()
        {
            if (SelectedInstanceId != null && objects.TryGetValue(SelectedInstanceId, out var go)) cameraRig.Frame(go.GetComponentInChildren<Renderer>().bounds);
            else FrameRoom();
        }
        public void ToggleGrid() { gridVisible = !gridVisible; gridRoot.SetActive(gridVisible); Root.Q<Button>("grid-toggle").text = gridVisible ? "눈금 켜짐 · G" : "눈금 꺼짐 · G"; }
        public void CycleWalls()
        {
            wallMode = (wallMode + 1) % 3;
            for (int i = 0; i < walls.Count; i++) walls[i].SetActive(wallMode == 0 || wallMode == 1 && i < 2);
            Root.Q<Button>("walls").text = "벽: " + new[] { "전체", "단면", "숨김" }[wallMode];
        }
        public void SelectAt(Vector2 screen)
        {
            if (!cameraRig.View.pixelRect.Contains(screen)) return;
            if (Physics.Raycast(cameraRig.View.ScreenPointToRay(screen), out var hit, 150))
            { var target = hit.collider.GetComponentInParent<DecorSelectable>(); if (target != null) { SelectInstance(target.InstanceId); return; } }
            SelectedInstanceId = null; ClearOutline(); Root.Q<Label>("selection-title").text = "물건을 선택해 보세요";
            Root.Q<Label>("selection-body").text = "방 안의 전시대와 꽃병을 각각 클릭해 보세요."; Root.Q("swatches").Clear(); Root.Q("parent").style.display = DisplayStyle.None;
            SelectionChanged?.Invoke(null);
        }
        public void SelectInstance(string id, bool notify = true)
        {
            var item = Document.world.instances.FirstOrDefault(i => i.id == id); if (item == null) return;
            SelectedInstanceId = id; ShowDefinition(Definition(item), item); ClearOutline();
            if (objects.TryGetValue(id, out var go)) Outline(go.GetComponentInChildren<Renderer>().bounds, ColorOf(Settings.gold));
            foreach (var child in Document.world.instances.Where(i => i.location.kind == "parent" && i.location.parentId == id))
                if (objects.TryGetValue(child.id, out var childObject)) Outline(childObject.GetComponentInChildren<Renderer>().bounds, ColorOf(Settings.accent));
            if (notify) SelectionChanged?.Invoke(item); RefreshTools();
        }
        public void ShowDefinition(ItemDefinition definition, ItemInstance item = null)
        {
            Root.Q<Label>("selection-title").text = definition.name;
            string dimensions = string.Join(" × ", definition.dimensionsM.Select(n => (n * 100).ToString("0.#", CultureInfo.InvariantCulture))) + " cm";
            Root.Q<Label>("selection-body").text = (definition.kind == "floorFurniture" ? "바닥 가구" : "상판 소품") + "\n" + dimensions + "\n" +
                (item == null ? "방 안을 가리킨 뒤 클릭해서 놓으세요." : item.location.kind == "storage" ? "보관 중 · 방 안을 클릭해 다시 놓으세요." : item.location.kind == "parent" ? "받침대 위에 전시 중" : "방에 전시 중") +
                (item == null ? "" : "\n색상: " + definition.variants.First(v => v.id == item.variantId).label);
            var swatches = Root.Q("swatches"); swatches.Clear();
            foreach (var variant in definition.variants)
            { var chip = new Button(() => Editor.Color(variant.id)) { tooltip = variant.label + " · 색상 적용" }; chip.AddToClassList("swatch"); chip.style.minWidth = chip.style.minHeight = 26; chip.style.paddingLeft = chip.style.paddingRight = 0; chip.style.backgroundColor = ColorOf(variant.colorHex); swatches.Add(chip); }
            Root.Q("parent").style.display = item?.location.kind == "parent" ? DisplayStyle.Flex : DisplayStyle.None;
            if (compact) { detailsOpen = true; Layout(); }
        }
        void RefreshCards()
        {
            cards.Clear(); string query = search.value?.Trim() ?? ""; int count = 0;
            Action<ItemDefinition, ItemInstance, string> add = (definition, item, subtitle) =>
            {
                if ((filter.Length > 0 && definition.kind != filter) || definition.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) return;
                var card = new Button(() => { if (item == null) { SelectedInstanceId = null; ClearOutline(); ShowDefinition(definition); CatalogSelected?.Invoke(definition); } else SelectInstance(item.id); });
                card.AddToClassList("card"); card.style.width = Settings.cardWidth; card.style.height = Settings.cardHeight; card.tooltip = definition.name + " · " + subtitle;
                var thumb = new VisualElement(); thumb.AddToClassList("card-thumbnail");
                thumb.style.backgroundColor = ColorOf((item == null ? definition.variants[0] : definition.variants.First(v => v.id == item.variantId)).colorHex); card.Add(thumb);
                var name = new Label(definition.name); name.AddToClassList("card-name"); card.Add(name);
                var sub = new Label(subtitle); sub.AddToClassList("card-subtitle"); card.Add(sub); cards.Add(card); count++;
            };
            if (inventory)
            {
                foreach (var group in (Editor == null ? DecorInventoryQuery.Read(Document.catalog, Document.world) : Editor.Interaction.Inventory()).storedGroups)
                { var item = Document.world.instances.First(i => i.id == group.rootIds[0]); add(Definition(item), item, group.rootCount + "개" + (group.hasChildren ? " · 소품 포함" : " · 보관 중")); }
            }
            else foreach (var definition in Document.catalog.items) add(definition, null, definition.kind == "floorFurniture" ? "바닥 가구" : "상판 소품");
            if (count == 0) cards.Add(new Label(inventory ? "해당하는 보관 물건이 없습니다." : "검색 결과가 없습니다."));
            Root.Q<Button>("catalog-tab").style.color = ColorOf(inventory ? Settings.muted : Settings.accent);
            Root.Q<Button>("inventory-tab").style.color = ColorOf(inventory ? Settings.accent : Settings.muted);
            Layout();
        }
        ItemDefinition Definition(ItemInstance item) { return Document.catalog.items.First(d => d.id == item.definition.id && d.version == item.definition.version); }
        static Vector3 V(double[] v) { return new Vector3((float)v[0], (float)v[1], (float)v[2]); }
        static Color ColorOf(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
        Material Material(Color color, string shader = "Standard")
        { string key = shader + ColorUtility.ToHtmlStringRGBA(color); if (sharedMaterials.TryGetValue(key, out var cached)) return cached; var m = new Material(Shader.Find(shader)); m.color = color; materials.Add(m); sharedMaterials.Add(key, m); return m; }
        GameObject Box(string name, Vector3 center, Vector3 size, Color color, Transform parent)
        { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = Material(color); return go; }
        void BuildWorld()
        {
            worldRoot = new GameObject("Decor fixture world"); worldRoot.transform.SetParent(transform, false);
            Box("Floor", new Vector3(0, -.08f, 0), new Vector3(20, .16f, 16), ColorOf(Settings.floor), worldRoot.transform);
            var wallColor = ColorOf(Settings.wall);
            walls.Add(Box("Back wall", new Vector3(0, 1.4f, 8), new Vector3(20, 2.8f, .15f), wallColor, worldRoot.transform));
            walls.Add(Box("Left wall", new Vector3(-10, 1.4f, 0), new Vector3(.15f, 2.8f, 16), wallColor, worldRoot.transform));
            walls.Add(Box("Front wall", new Vector3(0, 1.4f, -8), new Vector3(20, 2.8f, .15f), wallColor, worldRoot.transform));
            walls.Add(Box("Right wall", new Vector3(10, 1.4f, 0), new Vector3(.15f, 2.8f, 16), wallColor, worldRoot.transform));
            walls[2].SetActive(false); walls[3].SetActive(false);
            gridRoot = new GameObject("1 metre display grid"); gridRoot.transform.SetParent(worldRoot.transform, false);
            var gridMat = Material(new Color(.36f, .43f, .36f, .55f), "Sprites/Default");
            for (int x = -10; x <= 10; x++) Line(new Vector3(x, .006f, -8), new Vector3(x, .006f, 8), gridMat, .012f, gridRoot.transform);
            for (int z = -8; z <= 8; z++) Line(new Vector3(-10, .006f, z), new Vector3(10, .006f, z), gridMat, .012f, gridRoot.transform);
            itemRoot = new GameObject("Active decor items"); itemRoot.transform.SetParent(worldRoot.transform, false);
            foreach (var item in Document.world.instances.Where(i => i.location.kind == "room")) CreateItem(item, itemRoot.transform);
            foreach (var item in Document.world.instances.Where(i => i.location.kind == "parent"))
                if (objects.TryGetValue(item.location.parentId, out var parent)) CreateItem(item, parent.transform);
        }
        void CreateItem(ItemInstance item, Transform parent, IDictionary<string, GameObject> target = null)
        {
            var definition = Definition(item); var root = new GameObject(definition.name); root.transform.SetParent(parent, false);
            root.transform.localPosition = V(item.location.kind == "parent" ? item.location.localPositionM : item.location.positionM);
            root.transform.localRotation = Quaternion.Euler(0, (float)item.location.yawDeg.Value, 0);
            root.AddComponent<DecorSelectable>().InstanceId = item.id;
            Color color = ColorOf(definition.variants.First(v => v.id == item.variantId).colorHex);
            if (definition.kind == "surfaceDecor")
            {
                var vase = GameObject.CreatePrimitive(PrimitiveType.Cylinder); vase.name = definition.name; vase.transform.SetParent(root.transform, false);
                vase.transform.localPosition = V(definition.collision.centerM); var size = V(definition.dimensionsM); vase.transform.localScale = new Vector3(size.x, size.y * .5f, size.z); vase.GetComponent<Renderer>().sharedMaterial = Material(color);
            }
            else Box(definition.name, V(definition.collision.centerM), V(definition.dimensionsM), color, root.transform);
            (target ?? objects).Add(item.id, root);
        }
        GameObject Line(Vector3 a, Vector3 b, Material material, float width, Transform parent)
        {
            var go = new GameObject("Guide"); go.transform.SetParent(parent, false); var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.startWidth = line.endWidth = width; line.positionCount = 2; line.SetPositions(new[] { a, b }); return go;
        }
        void Outline(Bounds bounds, Color color)
        {
            bounds.Expand(.018f);
            if (!outlineMaterials.TryGetValue(color, out var mat)) { mat = Material(color, "Sprites/Default"); outlineMaterials.Add(color, mat); }
            var points = new Vector3[8]; for (int i = 0; i < 8; i++) points[i] = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
            for (int i = 0; i < 8; i++) for (int bit = 1; bit <= 4; bit *= 2) if ((i & bit) == 0) outlines.Add(Line(points[i], points[i | bit], mat, .012f, worldRoot.transform));
        }
        void ClearOutline() { foreach (var go in outlines) Destroy(go); outlines.Clear(); }
        public void ClearSelection()
        { SelectedInstanceId = null; ClearOutline(); Root.Q<Label>("selection-title").text = "물건을 선택해 보세요"; Root.Q<Label>("selection-body").text = "물건을 고르거나 카탈로그에서 새로 놓으세요."; Root.Q("swatches").Clear(); Root.Q("parent").style.display = DisplayStyle.None; RefreshTools(); }
        public void RefreshWorld(DecorWorld world, string selected)
        {
            Document.world = world; ClearOutline();
            foreach (var go in objects.Values.Where(g => g != null && g.transform.parent == itemRoot.transform).ToArray()) { go.SetActive(false); Destroy(go); }
            objects.Clear();
            foreach (var item in world.instances.Where(i => i.location.kind == "room")) CreateItem(item, itemRoot.transform);
            foreach (var item in world.instances.Where(i => i.location.kind == "parent")) if (objects.TryGetValue(item.location.parentId, out var parent)) CreateItem(item, parent.transform);
            RefreshCards(); ClearSelection(); if (selected != null) SelectInstance(selected, false);
        }
        public GameObject ObjectFor(string id) { objects.TryGetValue(id, out var value); return value; }
        public void RefreshTools()
        {
            if (Editor == null) return; var edit = Editor.Interaction; var item = Document.world.instances.FirstOrDefault(i => i.id == SelectedInstanceId);
            Root.Q<Button>("move-item").SetEnabled(item != null && item.location.kind != "storage" && !edit.HasPreview);
            Root.Q<Button>("store-item").SetEnabled(item != null && item.location.kind != "storage" && !edit.HasPreview);
            Root.Q<Button>("rotate-left").SetEnabled(edit.HasPreview || item != null && item.location.kind != "storage");
            Root.Q<Button>("rotate-right").SetEnabled(edit.HasPreview || item != null && item.location.kind != "storage");
            Root.Q<Button>("undo").SetEnabled(edit.UndoCount > 0); Root.Q<Button>("redo").SetEnabled(edit.RedoCount > 0);
            Root.Q<Button>("cancel-preview").SetEnabled(edit.HasPreview);
            if (Session != null && !Session.CanEdit) foreach (var id in new[] { "move-item", "store-item", "rotate-left", "rotate-right", "undo", "redo", "cancel-preview" }) Root.Q<Button>(id).SetEnabled(false);
            foreach (var chip in Root.Q("swatches").Children().OfType<Button>()) chip.SetEnabled(Session == null || Session.CanEdit);
        }
        public void ShowWorld(bool visible) { worldRoot.SetActive(visible); }
        public IDecorPreparedView PrepareWorld(DecorWorld world) { return new PreparedItems(this, world); }
        sealed class PreparedItems : IDecorPreparedView
        {
            readonly DecorPanel panel; readonly Dictionary<string, GameObject> next = new Dictionary<string, GameObject>();
            GameObject root; bool activated;
            internal PreparedItems(DecorPanel panel, DecorWorld world)
            {
                this.panel = panel; root = new GameObject("Prepared decor items"); root.SetActive(false); root.transform.SetParent(panel.worldRoot.transform, false);
                try
                {
                    foreach (var item in world.instances.Where(i => i.location.kind == "room")) panel.CreateItem(item, root.transform, next);
                    foreach (var item in world.instances.Where(i => i.location.kind == "parent")) if (next.TryGetValue(item.location.parentId, out var parent)) panel.CreateItem(item, parent.transform, next);
                }
                catch { UnityEngine.Object.Destroy(root); root = null; throw; }
            }
            public void Activate(DecorWorld world)
            {
                panel.ClearOutline(); panel.itemRoot.SetActive(false); UnityEngine.Object.Destroy(panel.itemRoot);
                panel.itemRoot = root; panel.objects.Clear(); foreach (var pair in next) panel.objects.Add(pair.Key, pair.Value);
                panel.Document.world = world; root.SetActive(true); activated = true; panel.ShowWorld(true); panel.RefreshCards(); panel.ClearSelection();
            }
            public void Dispose() { if (!activated && root != null) UnityEngine.Object.Destroy(root); }
        }
        void OnDestroy() { foreach (var m in materials) Destroy(m); if (fontAsset != null) Destroy(fontAsset); }
    }
}
