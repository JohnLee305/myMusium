using System;
using System.Collections.Generic;

namespace FamilyMuseum.Decor
{
    // Wire names deliberately match decor-slice-1. No Unity objects or file I/O.
    [Serializable] public sealed class DecorDocument { public DecorCatalog catalog; public DecorWorld world; }
    [Serializable] public sealed class DecorCatalog { public string revision; public ItemDefinition[] items; }
    [Serializable] public sealed class DefinitionRef { public string id; public int version; }
    [Serializable] public sealed class DecorBox { public double[] centerM; public double[] sizeM; }
    [Serializable] public sealed class SupportSurface { public string id; public double[] centerM; public double[] sizeXZ; }
    [Serializable] public sealed class ItemVariant { public string id; public string label; public string colorHex; }
    [Serializable] public sealed class ItemDefinition
    {
        public string id; public int version; public string name; public string kind;
        public double[] dimensionsM; public DecorBox collision; public SupportSurface[] supports;
        public double snapM; public ItemVariant[] variants;
    }
    [Serializable] public sealed class DecorRoom { public string id; public string name; public DecorBox bounds; public DecorBox[] reservedZones; }
    [Serializable] public sealed class ItemLocation
    {
        public string kind;
        // Only the branch selected by kind may be populated. Null means absent, not zero.
        public string roomId; public double[] positionM;
        public string parentId; public string surfaceId; public double[] localPositionM;
        public double? yawDeg;
    }
    [Serializable] public sealed class ItemInstance
    {
        public string id; public DefinitionRef definition; public string variantId; public ItemLocation location;
    }
    [Serializable] public sealed class DecorWorld
    {
        public string schemaVersion; public string id; public int revision; public string catalogRevision;
        public string lastCommandBatchId; public DecorRoom[] rooms; public ItemInstance[] instances;
    }
    public sealed class ValidationIssue
    {
        public string code; public string primaryId; public string[] relatedIds;
        public string roomId; public double[] position;
        public ValidationIssue(string code, string primaryId, params string[] relatedIds)
        { this.code = code; this.primaryId = primaryId; this.relatedIds = relatedIds ?? new string[0]; }
    }
    public sealed class ValidationReport
    {
        public readonly ValidationIssue[] issues;
        public bool ok { get { return issues.Length == 0; } }
        public ValidationReport(IEnumerable<ValidationIssue> values)
        { var list = new List<ValidationIssue>(values); list.Sort(Compare); issues = list.ToArray(); }
        static int Compare(ValidationIssue a, ValidationIssue b)
        {
            int n = StringComparer.Ordinal.Compare(a.code, b.code);
            if (n == 0) n = StringComparer.Ordinal.Compare(a.primaryId, b.primaryId);
            if (n == 0) n = StringComparer.Ordinal.Compare(string.Join("\0", a.relatedIds), string.Join("\0", b.relatedIds));
            return n;
        }
    }
}
