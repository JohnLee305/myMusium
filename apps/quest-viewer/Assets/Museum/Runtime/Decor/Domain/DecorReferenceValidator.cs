using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FamilyMuseum.Decor
{
    // DECOR-01 checks contract stages 1..5 only. Geometry belongs to DECOR-04.
    public static class DecorReferenceValidator
    {
        public static ValidationReport ValidateJson(string json, out DecorDocument document)
        {
            ValidationReport shape;
            if (!DecorJson.TryRead(json, out document, out shape)) return shape;
            return Validate(document.catalog, document.world);
        }
        public static ValidationReport Validate(DecorCatalog catalog, DecorWorld world)
        {
            try { DecorJson.CheckShape(catalog, world); }
            catch (InvalidDataException) { return new ValidationReport(new[] { new ValidationIssue("SCHEMA_INVALID", "document") }); }
            var issues = new List<ValidationIssue>();
            if (world.catalogRevision != catalog.revision) issues.Add(new ValidationIssue("CATALOG_REVISION_MISMATCH", world.id, catalog.revision, world.catalogRevision));

            var definitions = Unique(catalog.items, Key, "DUPLICATE_DEFINITION", issues);
            var rooms = Unique(world.rooms, r => r.id, "DUPLICATE_ROOM_ID", issues);
            var instances = Unique(world.instances, i => i.id, "DUPLICATE_INSTANCE_ID", issues);
            foreach (var d in catalog.items)
            {
                foreach (var group in d.supports.GroupBy(s => s.id, StringComparer.Ordinal).Where(g => g.Count() > 1))
                    issues.Add(new ValidationIssue("DUPLICATE_SUB_ID", Key(d), "supports", group.Key));
                foreach (var group in d.variants.GroupBy(v => v.id, StringComparer.Ordinal).Where(g => g.Count() > 1))
                    issues.Add(new ValidationIssue("DUPLICATE_SUB_ID", Key(d), "variants", group.Key));
            }
            var definitionKeys = new HashSet<string>(catalog.items.Select(Key), StringComparer.Ordinal);
            var roomIds = new HashSet<string>(world.rooms.Select(r => r.id), StringComparer.Ordinal);
            var instanceIds = new HashSet<string>(world.instances.Select(i => i.id), StringComparer.Ordinal);
            var resolved = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var i in instances.Values)
            {
                ItemDefinition d; string key = Key(i.definition.id, i.definition.version);
                if (!definitionKeys.Contains(key)) issues.Add(Issue("DEFINITION_NOT_FOUND", i, key));
                else if (definitions.TryGetValue(key, out d))
                {
                    resolved.Add(i.id, d);
                    if (!d.variants.Any(v => v.id == i.variantId)) issues.Add(Issue("VARIANT_NOT_FOUND", i, i.variantId));
                }
                if (i.location.kind == "room" && !roomIds.Contains(i.location.roomId)) issues.Add(Issue("ROOM_NOT_FOUND", i, i.location.roomId));
                if (i.location.kind == "parent" && !instanceIds.Contains(i.location.parentId)) issues.Add(Issue("PARENT_NOT_FOUND", i, i.location.parentId));
            }
            // Detect cycles iteratively before checking kind/support. No recursion on untrusted graphs.
            var cycles = Cycles(instances);
            foreach (string id in cycles) issues.Add(Issue("PARENT_CYCLE", instances[id], instances[id].location.parentId));
            foreach (var i in instances.Values)
            {
                ItemDefinition d;
                if (!resolved.TryGetValue(i.id, out d) || cycles.Contains(i.id)) continue;
                var l = i.location;
                if ((l.kind == "room" && d.kind != "floorFurniture") || (l.kind == "parent" && d.kind != "surfaceDecor"))
                    issues.Add(Issue("SURFACE_KIND_INVALID", i));
                if (l.kind != "parent") continue;
                ItemInstance parent; ItemDefinition parentDefinition;
                if (!instances.TryGetValue(l.parentId, out parent) || cycles.Contains(parent.id) || !resolved.TryGetValue(parent.id, out parentDefinition)) continue;
                if (parent.location.kind == "parent") { issues.Add(Issue("PARENT_DEPTH_INVALID", i, parent.id)); continue; }
                if (parentDefinition.kind != "floorFurniture") { issues.Add(Issue("SURFACE_KIND_INVALID", i, parent.id)); continue; }
                if (!parentDefinition.supports.Any(s => s.id == l.surfaceId)) issues.Add(Issue("SUPPORT_NOT_FOUND", i, parent.id, l.surfaceId));
            }
            return new ValidationReport(issues);
        }
        static string Key(ItemDefinition d) { return Key(d.id, d.version); }
        static string Key(string id, int version) { return id + "@" + version.ToString(CultureInfo.InvariantCulture); }
        static ValidationIssue Issue(string code, ItemInstance item, params string[] related)
        {
            var issue = new ValidationIssue(code, item.id, related);
            if (item.location.kind == "room") { issue.roomId = item.location.roomId; issue.position = (double[])item.location.positionM.Clone(); }
            return issue;
        }
        static Dictionary<string, T> Unique<T>(IEnumerable<T> values, Func<T, string> key, string code, List<ValidationIssue> issues)
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var group in values.GroupBy(key, StringComparer.Ordinal))
            {
                if (group.Count() > 1) issues.Add(new ValidationIssue(code, group.Key));
                else result.Add(group.Key, group.First());
            }
            return result; // Ambiguous keys are never silently resolved by first/last-wins.
        }
        static HashSet<string> Cycles(Dictionary<string, ItemInstance> instances)
        {
            var done = new HashSet<string>(StringComparer.Ordinal);
            var cycles = new HashSet<string>(StringComparer.Ordinal);
            foreach (string start in instances.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var path = new List<string>(); var positions = new Dictionary<string, int>(StringComparer.Ordinal); string cursor = start;
                while (!done.Contains(cursor))
                {
                    int index;
                    if (positions.TryGetValue(cursor, out index)) { for (int n = index; n < path.Count; n++) cycles.Add(path[n]); break; }
                    ItemInstance current;
                    if (!instances.TryGetValue(cursor, out current)) break;
                    positions.Add(cursor, path.Count); path.Add(cursor);
                    if (current.location.kind != "parent") break;
                    cursor = current.location.parentId;
                }
                foreach (string id in path) done.Add(id);
            }
            return cycles;
        }
    }
}
