using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FamilyMuseum.Decor
{
    public sealed class DecorInventoryGroup
    {
        public string definitionId, variantId;
        public int definitionVersion, rootCount, memberCount;
        public bool hasChildren;
        public string[] rootIds;
    }
    public sealed class DecorInventorySummary
    {
        public int ownedCount, storedCount, placedCount;
        public DecorInventoryGroup[] storedGroups;
    }
    public static class DecorInventoryQuery
    {
        // Counts include attached children; stored cards represent roots and never create new ownership.
        public static DecorInventorySummary Read(DecorCatalog catalog, DecorWorld world, IDictionary<string, long> lastStored = null)
        {
            if (!DecorReferenceValidator.Validate(catalog, world).ok) throw new InvalidDataException("INVENTORY_REFERENCE_INVALID");
            var index = world.instances.ToDictionary(i => i.id, StringComparer.Ordinal);
            int stored = 0;
            foreach (var item in world.instances)
            {
                var root = item; while (root.location.kind == "parent") root = index[root.location.parentId];
                if (root.location.kind == "storage") stored++;
            }
            var groups = new List<DecorInventoryGroup>();
            foreach (var group in world.instances.Where(i => i.location.kind == "storage").GroupBy(i => AssemblyKey(i, world), StringComparer.Ordinal))
            {
                var ordered = group.OrderByDescending(i => Recency(lastStored, i.id)).ThenBy(i => i.id, StringComparer.Ordinal).ToArray();
                var first = ordered[0]; int members = group.Sum(i => 1 + Children(i, world).Count());
                groups.Add(new DecorInventoryGroup { definitionId = first.definition.id, definitionVersion = first.definition.version, variantId = first.variantId,
                    rootCount = ordered.Length, memberCount = members, hasChildren = Children(first, world).Any(), rootIds = ordered.Select(i => i.id).ToArray() });
            }
            return new DecorInventorySummary { ownedCount = world.instances.Length, storedCount = stored, placedCount = world.instances.Length - stored,
                storedGroups = groups.OrderByDescending(g => Recency(lastStored, g.rootIds[0])).ThenBy(g => g.rootIds[0], StringComparer.Ordinal).ToArray() };
        }
        static long Recency(IDictionary<string, long> map, string id) { long n; return map != null && map.TryGetValue(id, out n) ? n : 0; }
        static IEnumerable<ItemInstance> Children(ItemInstance parent, DecorWorld world)
        { return world.instances.Where(i => i.location.kind == "parent" && i.location.parentId == parent.id); }
        static string AssemblyKey(ItemInstance root, DecorWorld world)
        {
            var children = Children(root, world).Select(i => DecorKeys.Definition(i.definition) + DecorKeys.Token(i.variantId)
                + DecorKeys.Token(i.location.surfaceId) + DecorKeys.Vector(i.location.localPositionM) + DecorKeys.Token(DecorKeys.Number(i.location.yawDeg.Value)))
                .OrderBy(s => s, StringComparer.Ordinal).Select(DecorKeys.Token);
            return DecorKeys.Definition(root.definition) + DecorKeys.Token(root.variantId) + DecorKeys.Token(string.Join("", children));
        }
    }
}
