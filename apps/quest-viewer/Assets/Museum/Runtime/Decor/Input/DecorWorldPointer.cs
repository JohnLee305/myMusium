using System;
using System.Linq;

namespace FamilyMuseum.Decor
{
    public enum DecorHitRole { Solid, Floor, Item, UI, Preview, PlayerProxy }
    public sealed class DecorSceneHit
    {
        public DecorHitRole role;
        public double distanceM;
        public double[] positionM, normal;
        public string instanceId, roomId;
    }
    public interface IDecorRaycastScene
    {
        // Adapter uses a normalized ray and a bounded query. Include visible walls and UI blockers.
        DecorSceneHit[] Raycast(DecorPointerRay normalizedRay);
    }
    // Shared candidate conversion. Physics/scene acquisition is an adapter; no screen coordinates here.
    public sealed class DecorWorldPointer : IDecorWorldPointer
    {
        readonly DecorCatalog catalog;
        readonly IDecorRaycastScene scene;
        public DecorWorldPointer(DecorCatalog catalog, IDecorRaycastScene scene)
        { this.catalog = DecorCopies.Catalog(catalog); this.scene = scene ?? throw new ArgumentNullException(nameof(scene)); }
        public DecorPointerHit Query(DecorPointerRay ray, DecorWorld world, ItemInstance preview, bool snap)
        {
            if (ray == null || !ray.Valid) return Block("TRACKED_RAY_REQUIRED");
            double length = Math.Sqrt(ray.direction.Sum(v => v * v));
            var normalized = new DecorPointerRay { originM = (double[])ray.originM.Clone(), direction = ray.direction.Select(v => v / length).ToArray(), maxDistanceM = ray.maxDistanceM, tracked = true };
            var hits = scene.Raycast(normalized);
            if (hits == null) return Block("TARGET_REQUIRED");
            var hit = hits.Where(h => h != null && h.distanceM >= 0 && h.distanceM <= ray.maxDistanceM &&
                    h.role != DecorHitRole.Preview && h.role != DecorHitRole.PlayerProxy && !IsMovingMember(h.instanceId, preview, world))
                // A coincident blocker wins; never pick a floor through a zero-thickness wall/UI.
                .OrderBy(h => h.distanceM).ThenBy(h => h.role == DecorHitRole.Solid || h.role == DecorHitRole.UI ? 0 : 1).FirstOrDefault();
            if (hit == null) return Block("TARGET_REQUIRED");
            if (hit.role == DecorHitRole.Solid || hit.role == DecorHitRole.UI) return Block("TARGET_OCCLUDED");
            if (!Vector(hit.positionM) || !Vector(hit.normal)) return Block("HIT_INVALID");
            var found = world.instances.FirstOrDefault(i => i.id == hit.instanceId && i.location.kind != "storage");
            if (preview == null) return found == null ? Block("ITEM_NOT_FOUND") : new DecorPointerHit { instanceId = found.id };
            var definition = Definition(preview);
            if (definition.kind == "floorFurniture")
            {
                if (hit.role != DecorHitRole.Floor || hit.normal[1] < .9) return Block("VISIBLE_FLOOR_REQUIRED");
                var room = world.rooms.FirstOrDefault(r => r.id == hit.roomId);
                if (room == null) return Block("ROOM_REQUIRED");
                double floor = room.bounds.centerM[1] - room.bounds.sizeM[1] / 2;
                if (Math.Abs(hit.positionM[1] - floor) > DecorGeometry.ToleranceM) return Block("FLOOR_HEIGHT_INVALID");
                var origin = new[] { room.bounds.centerM[0], floor, room.bounds.centerM[2] };
                var position = Position(hit.positionM, origin, definition.snapM, snap); position[1] = floor;
                return new DecorPointerHit { target = new ItemLocation { kind = "room", roomId = room.id, positionM = position, yawDeg = preview.location.yawDeg ?? 0 } };
            }
            if (hit.role != DecorHitRole.Item || found == null || Definition(found).kind != "floorFurniture" || found.location.kind != "room" || hit.normal[1] < .9)
                return Block("SUPPORT_REQUIRED");
            var local = DecorGeometry.TransformPoint(new double[3], DecorGeometry.NormalizeYaw(360 - found.location.yawDeg.Value),
                hit.positionM.Select((v, index) => v - found.location.positionM[index]).ToArray());
            var support = Definition(found).supports.FirstOrDefault(s => Math.Abs(local[1] - s.centerM[1]) <= .015 &&
                Math.Abs(local[0] - s.centerM[0]) <= s.sizeXZ[0] / 2 && Math.Abs(local[2] - s.centerM[2]) <= s.sizeXZ[1] / 2);
            if (support == null) return Block("SUPPORT_REQUIRED");
            var targetLocal = Position(local, support.centerM, definition.snapM, snap); targetLocal[1] = support.centerM[1];
            return new DecorPointerHit { instanceId = found.id, target = new ItemLocation { kind = "parent", parentId = found.id,
                surfaceId = support.id, localPositionM = targetLocal, yawDeg = preview.location.yawDeg ?? 0 } };
        }
        ItemDefinition Definition(ItemInstance item) { return catalog.items.Single(d => d.id == item.definition.id && d.version == item.definition.version); }
        static bool IsMovingMember(string id, ItemInstance preview, DecorWorld world)
        {
            if (id == null || preview == null) return false;
            if (id == preview.id) return true;
            var found = world.instances.FirstOrDefault(i => i.id == id);
            return found != null && found.location.kind == "parent" && found.location.parentId == preview.id;
        }
        static bool Vector(double[] vector) { return vector != null && vector.Length == 3 && vector.All(v => !double.IsNaN(v) && !double.IsInfinity(v)); }
        static double[] Position(double[] p, double[] origin, double step, bool snap)
        { return snap ? DecorGeometry.SnapPosition(p, origin, step) : p.Select(DecorGeometry.QuantizeM).ToArray(); }
        static DecorPointerHit Block(string reason) { return new DecorPointerHit { blocked = true, reasonCode = reason }; }
    }
}
