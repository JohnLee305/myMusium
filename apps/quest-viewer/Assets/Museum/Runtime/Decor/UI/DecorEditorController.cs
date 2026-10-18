using System;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum.Decor
{
    public sealed class DecorEditorController : MonoBehaviour
    {
        public DecorInteraction Interaction { get; private set; }
        public DecorPlacementPreview Preview { get; private set; }
        DecorPanel panel;
        bool eyedropper;
        public void Eyedropper() { Cancel(); eyedropper = true; panel.SetStatus("스포이트 · 복사할 물건을 클릭하세요. 기존 물건과 소품은 복제되지 않습니다."); }
        public void Initialize(DecorPanel value)
        {
            panel = value; Interaction = new DecorInteraction(panel.Document.catalog, panel.Document.world);
            Preview = gameObject.AddComponent<DecorPlacementPreview>();
            panel.CatalogSelected += New; panel.SelectionChanged += Selected;
        }
        void New(ItemDefinition definition) { Interaction.BeginNew(definition); panel.ShowDefinition(definition); Message(); }
        void Selected(ItemInstance item)
        {
            Interaction.Select(item?.id); Preview.Clear();
            if (item?.location.kind == "storage") { Interaction.BeginMove(item.id); Message(); }
            panel.RefreshTools();
        }
        public void MoveSelected()
        { if (Interaction.BeginMove(panel.SelectedInstanceId)) { Preview.Show(Interaction); Message(); } }
        public void UpdatePointer(Vector2 screen, bool free)
        {
            if (!Interaction.HasPreview) return;
            if (!panel.cameraRig.View.pixelRect.Contains(screen)) { Interaction.Invalidate("TARGET_REQUIRED"); Preview.Clear(); return; }
            var ray = panel.cameraRig.View.ScreenPointToRay(screen); var item = Interaction.PreviewItem; var definition = Interaction.Definition(item);
            if (definition.kind == "floorFurniture")
            {
                var room = panel.Document.world.rooms[0]; double floor = room.bounds.centerM[1] - room.bounds.sizeM[1] / 2;
                if (new Plane(Vector3.up, new Vector3(0, (float)floor, 0)).Raycast(ray, out float distance))
                {
                    var p = ray.GetPoint(distance); var vector = Position(p, new[] { room.bounds.centerM[0], floor, room.bounds.centerM[2] }, definition.snapM, free);
                    vector[1] = floor; Interaction.SetTarget(new ItemLocation { kind = "room", roomId = room.id, positionM = vector, yawDeg = Interaction.PreviewYaw });
                }
                else Interaction.Invalidate("TARGET_REQUIRED");
            }
            else
            {
                // A real hit on the visible support is required: do not attach through walls or to hidden planes.
                var hits = Physics.RaycastAll(ray, 150).OrderBy(h => h.distance);
                ItemInstance parent = null; RaycastHit picked = default;
                foreach (var hit in hits)
                {
                    var selectable = hit.collider.GetComponentInParent<DecorSelectable>();
                    if (selectable != null && selectable.InstanceId == item.id) continue;
                    if (selectable != null)
                    {
                        var found = panel.Document.world.instances.First(i => i.id == selectable.InstanceId);
                        if (Interaction.Definition(found).kind == "floorFurniture") { parent = found; picked = hit; }
                    }
                    break;
                }
                if (parent != null)
                {
                    DecorPlacementPreview.Frame(panel.Document.world, parent, out var pp, out var py);
                    var local = Quaternion.Euler(0, -py, 0) * (picked.point - pp); var support = Interaction.Definition(parent).supports.FirstOrDefault();
                    if (support != null && Mathf.Abs(local.y - (float)support.centerM[1]) <= .015f && picked.normal.y > .9f)
                    {
                        var vector = Position(local, support.centerM, definition.snapM, free); vector[1] = support.centerM[1];
                        Interaction.SetTarget(new ItemLocation { kind = "parent", parentId = parent.id, surfaceId = support.id, localPositionM = vector, yawDeg = Interaction.PreviewYaw });
                    }
                    else Interaction.Invalidate("SUPPORT_REQUIRED");
                }
                else Interaction.Invalidate("SUPPORT_REQUIRED");
            }
            Preview.Show(Interaction); Message();
        }
        static double[] Position(Vector3 p, double[] origin, double snap, bool free)
        { var v = new double[] { p.x, p.y, p.z }; return free ? v.Select(DecorGeometry.QuantizeM).ToArray() : DecorGeometry.SnapPosition(v, origin, snap); }
        public void Click(Vector2 screen, bool repeat)
        {
            if (eyedropper)
            {
                panel.SelectAt(screen); var picked = panel.Document.world.instances.FirstOrDefault(i => i.id == panel.SelectedInstanceId);
                if (picked != null) { eyedropper = false; Interaction.BeginNew(Interaction.Definition(picked), picked.variantId); panel.ShowDefinition(Interaction.Definition(picked)); Message(); }
                return;
            }
            if (!Interaction.HasPreview) { panel.SelectAt(screen); return; }
            if (!Interaction.PreviewValid) { Message(); return; }
            var result = Interaction.Commit(repeat); Apply(result, "물건을 놓았습니다. · Ctrl+Z 되돌리기");
        }
        public void Rotate(int direction)
        {
            var result = Interaction.RotateSelected(direction);
            if (Interaction.HasPreview) { Preview.Show(Interaction); Message(); }
            else Apply(result, "90도 회전했습니다.");
        }
        public void Store() { Apply(Interaction.StoreSelected(), "소품을 포함해 선택 물건을 보관했습니다. · Ctrl+Z 되돌리기"); }
        public void Color(string variant)
        {
            if (Interaction.HasPreview && !Interaction.IsCreatingPreview) { panel.SetStatus("이동이나 재설치를 확정하거나 취소한 뒤 색상을 바꾸세요."); return; }
            var result = Interaction.SetVariant(variant); if (Interaction.HasPreview) { Preview.Show(Interaction); Message(); } else Apply(result, "색상을 바꿨습니다.");
        }
        public void Undo() { Apply(Interaction.Undo(), "되돌렸습니다."); }
        public void Redo() { Apply(Interaction.Redo(), "다시 실행했습니다."); }
        public void Cancel()
        {
            eyedropper = false;
            if (Interaction.HasPreview) { Interaction.CancelPreview(); Preview.Clear(); panel.SetStatus("미리보기를 취소했습니다. 확정한 배치는 유지됩니다."); }
            else { panel.ClearSelection(); Interaction.Select(null); }
            panel.RefreshTools();
        }
        void Apply(DecorCommandResult result, string success)
        {
            if (result == null) return;
            if (!result.accepted) { panel.SetStatus(Reason(result.issues.FirstOrDefault()?.code)); return; }
            panel.RefreshWorld(Interaction.Snapshot(), Interaction.SelectedId); Preview.Show(Interaction);
            panel.SetStatus(success + (result.historyTrimmed ? " · 오래된 되돌리기 기록을 정리했습니다." : "")); panel.RefreshTools();
        }
        void Message()
        {
            panel.SetStatus(Interaction.PreviewValid ? "놓을 수 있습니다 · 클릭 확정 / Shift 연속 / Q·E 회전 / Alt 자유 위치 / Esc 취소" : Reason(Interaction.PreviewIssues.FirstOrDefault()?.code));
            panel.RefreshTools();
        }
        public static string Reason(string code)
        {
            switch (code)
            {
                case "ITEM_COLLISION": return "다른 물건과 겹칩니다. 붉은 미리보기를 다른 위치로 옮기세요.";
                case "RESERVED_ZONE_BLOCKED": return "문 앞 여유 공간을 비워 주세요.";
                case "OUTSIDE_ROOM": return "물건 전체가 방 안에 들어와야 합니다.";
                case "SUPPORT_OVERHANG": return "소품이 상판 밖으로 나갑니다.";
                case "SUPPORT_HEIGHT_INVALID": return "상판 높이에 맞춰 주세요.";
                case "SUPPORT_REQUIRED": return "전시대의 보이는 상판 위에 놓으세요.";
                case "TARGET_REQUIRED": return "방 안의 설치할 위치를 가리켜 주세요.";
                case "NOTHING_TO_UNDO": return "되돌릴 변경이 없습니다.";
                case "NOTHING_TO_REDO": return "다시 실행할 변경이 없습니다.";
                default: return "이 위치에는 놓을 수 없습니다. 위치나 설치 면을 확인해 주세요.";
            }
        }
    }
}
