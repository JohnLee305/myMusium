using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMuseum.Decor
{
    // Fixed-contract geometry stages 6..7 plus the reference gate. No rendering, Physics, navigation or mutation.
    public static class DecorPlacementValidator
    {
        public static DecorEditSession BeginEdit(DecorCatalog catalog, DecorWorld world, int historyLimit = 200)
        { return new DecorEditSession(catalog, world, Validate, historyLimit); }
        public static ValidationReport ValidateJson(string json, out DecorDocument document)
        { ValidationReport shape; return DecorJson.TryRead(json, out document, out shape) ? Validate(document.catalog, document.world) : shape; }
        sealed class Frame
        {
            internal ItemInstance item;
            internal string rootId, roomId;
            internal double[] position;
            internal double yaw;
            internal DecorBounds bounds;
        }
        public static ValidationReport Validate(DecorCatalog catalog, DecorWorld world)
        {
            var references = DecorReferenceValidator.Validate(catalog, world);
            if (!references.ok) return references; // Never guess geometry through ambiguous/broken references.
            var issues = new List<ValidationIssue>();
            var definitions = catalog.items.ToDictionary(Key, StringComparer.Ordinal);
            var items = world.instances.ToDictionary(i => i.id, StringComparer.Ordinal);
            var rooms = world.rooms.ToDictionary(r => r.id, StringComparer.Ordinal);
            var frames = new Dictionary<string, Frame>(StringComparer.Ordinal);
            var unsupported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in world.instances)
                if (item.location.kind != "storage" && !DecorGeometry.SupportedYaw(item.location.yawDeg.Value))
                { unsupported.Add(item.id); issues.Add(Issue("ROTATION_UNSUPPORTED", item.id)); }
            foreach (var item in world.instances.Where(i => i.location.kind != "parent"))
            {
                if (unsupported.Contains(item.id)) continue;
                bool placed = item.location.kind == "room";
                var f = new Frame { item = item, rootId = item.id, roomId = placed ? item.location.roomId : null,
                    position = placed ? (double[])item.location.positionM.Clone() : new double[3], yaw = placed ? item.location.yawDeg.Value : 0 };
                f.bounds = DecorGeometry.Bounds(definitions[Key(item.definition)].collision, f.position, f.yaw); frames.Add(item.id, f);
                if (placed)
                {
                    var room = rooms[f.roomId]; double floor = room.bounds.centerM[1] - room.bounds.sizeM[1] / 2;
                    if (DecorGeometry.Exceeds(Math.Abs(f.position[1] - floor))) issues.Add(Issue("FLOOR_HEIGHT_INVALID", f));
                }
            }
            foreach (var item in world.instances.Where(i => i.location.kind == "parent"))
            {
                Frame parent;
                if (unsupported.Contains(item.id) || !frames.TryGetValue(item.location.parentId, out parent)) continue;
                var definition = definitions[Key(item.definition)]; var host = definitions[Key(items[item.location.parentId].definition)];
                var surface = host.supports.Single(s => s.id == item.location.surfaceId);
                var local = DecorGeometry.Bounds(definition.collision, item.location.localPositionM, item.location.yawDeg.Value);
                var f = new Frame { item = item, rootId = parent.rootId, roomId = parent.roomId,
                    position = DecorGeometry.TransformPoint(parent.position, parent.yaw, item.location.localPositionM),
                    yaw = DecorGeometry.NormalizeYaw(parent.yaw + item.location.yawDeg.Value) };
                f.bounds = DecorGeometry.Bounds(definition.collision, f.position, f.yaw); frames.Add(item.id, f);
                if (DecorGeometry.Exceeds(Math.Abs(item.location.localPositionM[1] - surface.centerM[1])) || DecorGeometry.Exceeds(Math.Abs(local.minY - surface.centerM[1])))
                    issues.Add(Issue("SUPPORT_HEIGHT_INVALID", f, parent.item.id, surface.id));
                double minX = surface.centerM[0] - surface.sizeXZ[0] / 2, maxX = surface.centerM[0] + surface.sizeXZ[0] / 2;
                double minZ = surface.centerM[2] - surface.sizeXZ[1] / 2, maxZ = surface.centerM[2] + surface.sizeXZ[1] / 2;
                if (DecorGeometry.Exceeds(minX - local.minX) || DecorGeometry.Exceeds(local.maxX - maxX) || DecorGeometry.Exceeds(minZ - local.minZ) || DecorGeometry.Exceeds(local.maxZ - maxZ))
                    issues.Add(Issue("SUPPORT_OVERHANG", f, parent.item.id, surface.id));
            }
            foreach (var f in frames.Values.Where(f => f.roomId != null))
            {
                var room = rooms[f.roomId];
                if (!DecorGeometry.Contains(DecorGeometry.Bounds(room.bounds, new double[3], 0), f.bounds)) issues.Add(Issue("OUTSIDE_ROOM", f));
                if (room.reservedZones.Any(z => DecorGeometry.Overlaps(DecorGeometry.Bounds(z, new double[3], 0), f.bounds))) issues.Add(Issue("RESERVED_ZONE_BLOCKED", f));
            }
            var bodies = frames.Values.OrderBy(f => f.item.id, StringComparer.Ordinal).ToArray();
            for (int a = 0; a < bodies.Length; a++) for (int b = a + 1; b < bodies.Length; b++)
            {
                var left = bodies[a]; var right = bodies[b];
                // Stored assemblies have no common room. Only check each stored root's own contents.
                bool sameSpace = left.roomId != null ? left.roomId == right.roomId : right.roomId == null && left.rootId == right.rootId;
                if (sameSpace && DecorGeometry.Overlaps(left.bounds, right.bounds)) issues.Add(Issue("ITEM_COLLISION", left, right.item.id));
            }
            return new ValidationReport(issues);
        }
        static string Key(ItemDefinition d) { return Key(d.id, d.version); }
        static string Key(DefinitionRef d) { return Key(d.id, d.version); }
        static string Key(string id, int version) { return id + "@" + version.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        static ValidationIssue Issue(string code, string id) { return new ValidationIssue(code, id); }
        static ValidationIssue Issue(string code, Frame f, params string[] related)
        { return new ValidationIssue(code, f.item.id, related) { roomId = f.roomId, position = (double[])f.position.Clone() }; }
    }
}
