using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace FamilyMuseum.Decor
{
    // Strict JSON boundary for the small fixed contract; not a general JSON Schema engine.
    // Unknown/missing keys, duplicate keys, mixed location branches and non-finite numbers fail before references.
    public static class DecorJson
    {
        public static bool TryRead(string json, out DecorDocument document, out ValidationReport report)
        {
            document = null;
            try
            {
                var value = (DecorDocument)ConvertValue(new Parser(json).Read(), typeof(DecorDocument), "document");
                CheckShape(value.catalog, value.world); document = value;
                report = new ValidationReport(new ValidationIssue[0]); return true;
            }
            catch (InvalidDataException)
            { report = new ValidationReport(new[] { new ValidationIssue("SCHEMA_INVALID", "document") }); return false; }
        }
        public static string Write(DecorDocument document)
        {
            Need(document != null); CheckShape(document.catalog, document.world);
            var text = new StringBuilder(); WriteValue(text, document); return text.ToString();
        }
        public static bool TryReadWorld(string json, out DecorWorld world, out ValidationReport report)
        {
            world = null;
            try { var value = (DecorWorld)ConvertValue(new Parser(json).Read(), typeof(DecorWorld), "world"); CheckWorld(value); world = value; report = new ValidationReport(new ValidationIssue[0]); return true; }
            catch (InvalidDataException) { report = new ValidationReport(new[] { new ValidationIssue("SCHEMA_INVALID", "world") }); return false; }
        }
        public static bool TryReadCatalog(string json, out DecorCatalog catalog, out ValidationReport report)
        {
            catalog = null;
            try { var value = (DecorCatalog)ConvertValue(new Parser(json).Read(), typeof(DecorCatalog), "catalog"); CheckCatalog(value); catalog = value; report = new ValidationReport(new ValidationIssue[0]); return true; }
            catch (InvalidDataException) { report = new ValidationReport(new[] { new ValidationIssue("SCHEMA_INVALID", "catalog") }); return false; }
        }
        // Inspect only the root version after strict JSON parsing. Storage uses this to avoid overwriting newer formats.
        internal static bool TryInspectWorldVersion(string json, out string version)
        {
            version = null;
            try
            {
                var root = new Parser(json).Read() as Dictionary<string, object>; object value;
                if (root == null || !root.TryGetValue("schemaVersion", out value) || !(value is string)) return false;
                version = (string)value; return true;
            }
            catch (InvalidDataException) { return false; }
        }
        public static string WriteWorld(DecorWorld world) { CheckWorld(world); var text = new StringBuilder(); WriteValue(text, world); return text.ToString(); }
        public static string WriteCatalog(DecorCatalog catalog) { CheckCatalog(catalog); var text = new StringBuilder(); WriteValue(text, catalog); return text.ToString(); }
        internal static void Need(bool condition) { if (!condition) throw new InvalidDataException("SCHEMA_INVALID"); }
        static FieldInfo[] Fields(Type type) { return type.GetFields(BindingFlags.Public | BindingFlags.Instance); }
        static string[] LocationKeys(string kind)
        {
            if (kind == "room") return new[] { "kind", "roomId", "positionM", "yawDeg" };
            if (kind == "parent") return new[] { "kind", "parentId", "surfaceId", "localPositionM", "yawDeg" };
            Need(kind == "storage"); return new[] { "kind" };
        }
        static object ConvertValue(object value, Type type, string field)
        {
            if (value == null) { Need(field == "lastCommandBatchId"); return null; }
            if (type == typeof(string)) { Need(value is string); return value; }
            if (type == typeof(double) || type == typeof(double?)) { Need(value is double); return value; }
            if (type == typeof(int))
            { Need(value is double); double n = (double)value; Need(n >= int.MinValue && n <= int.MaxValue && n == Math.Truncate(n)); return (int)n; }
            if (type.IsArray)
            {
                Need(value is List<object>); var list = (List<object>)value;
                var array = Array.CreateInstance(type.GetElementType(), list.Count);
                for (int i = 0; i < list.Count; i++) array.SetValue(ConvertValue(list[i], type.GetElementType(), "element"), i);
                return array;
            }
            Need(value is Dictionary<string, object>); var obj = (Dictionary<string, object>)value;
            var keys = Fields(type).Select(f => f.Name).ToArray();
            if (type == typeof(ItemLocation))
            { Need(obj.ContainsKey("kind") && obj["kind"] is string); keys = LocationKeys((string)obj["kind"]); }
            Need(obj.Count == keys.Length && keys.All(obj.ContainsKey));
            object result = Activator.CreateInstance(type);
            foreach (string key in keys)
            { var f = type.GetField(key); f.SetValue(result, ConvertValue(obj[key], f.FieldType, key)); }
            return result;
        }
        static bool Id(string value) { return value != null && Regex.IsMatch(value, "^[a-z][a-z0-9.-]{0,63}$", RegexOptions.CultureInvariant); }
        static void Text(string value)
        {
            Need(value != null); int length = 0;
            for (int i = 0; i < value.Length; i++, length++)
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) i++;
            Need(length >= 1 && length <= 128); // JSON Schema length counts Unicode code points.
        }
        static void Vector(double[] value, int length, bool size)
        { Need(value != null && value.Length == length); foreach (double n in value) Need(!double.IsNaN(n) && !double.IsInfinity(n) && n <= 1000 && (size ? n > 0 : n >= -1000)); }
        static void Box(DecorBox box) { Need(box != null); Vector(box.centerM, 3, false); Vector(box.sizeM, 3, true); }
        static void ArrayLimit<T>(T[] values, int min, int max)
        { Need(values != null && values.Length >= min && values.Length <= max); foreach (T value in values) Need(value != null); }
        internal static void CheckShape(DecorCatalog catalog, DecorWorld world)
        {
            CheckCatalog(catalog); CheckWorld(world);
        }
        static void CheckCatalog(DecorCatalog catalog)
        {
            Need(catalog != null && Id(catalog.revision)); ArrayLimit(catalog.items, 1, 1000);
            foreach (var d in catalog.items)
            {
                Need(Id(d.id) && d.version >= 1 && (d.kind == "floorFurniture" || d.kind == "surfaceDecor")); Text(d.name);
                Vector(d.dimensionsM, 3, true); Box(d.collision); Need(d.snapM == .25 || d.snapM == .05);
                ArrayLimit(d.supports, 0, 8); ArrayLimit(d.variants, 1, 16);
                foreach (var s in d.supports) { Need(Id(s.id)); Vector(s.centerM, 3, false); Vector(s.sizeXZ, 2, true); }
                foreach (var v in d.variants) { Need(Id(v.id)); Text(v.label); Need(v.colorHex != null && Regex.IsMatch(v.colorHex, "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)); }
            }
        }
        static void CheckWorld(DecorWorld world)
        {
            Need(world != null);
            Need(world.schemaVersion == "decor-slice-1" && Id(world.id) && world.revision >= 0 && Id(world.catalogRevision));
            Need(world.lastCommandBatchId == null || Id(world.lastCommandBatchId)); ArrayLimit(world.rooms, 1, 8); ArrayLimit(world.instances, 0, 512);
            foreach (var r in world.rooms) { Need(Id(r.id)); Text(r.name); Box(r.bounds); ArrayLimit(r.reservedZones, 0, 16); foreach (var b in r.reservedZones) Box(b); }
            foreach (var i in world.instances)
            {
                Need(Id(i.id) && i.definition != null && Id(i.definition.id) && i.definition.version >= 1 && Id(i.variantId) && i.location != null);
                var l = i.location;
                if (l.kind == "storage") Need(l.roomId == null && l.positionM == null && l.parentId == null && l.surfaceId == null && l.localPositionM == null && !l.yawDeg.HasValue);
                else
                {
                    Need(l.yawDeg.HasValue && !double.IsNaN(l.yawDeg.Value) && l.yawDeg.Value >= 0 && l.yawDeg.Value < 360);
                    if (l.kind == "room") { Need(Id(l.roomId) && l.parentId == null && l.surfaceId == null && l.localPositionM == null); Vector(l.positionM, 3, false); }
                    else { Need(l.kind == "parent" && Id(l.parentId) && Id(l.surfaceId) && l.roomId == null && l.positionM == null); Vector(l.localPositionM, 3, false); }
                }
            }
        }
        static void WriteValue(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null"); return; }
            if (value is string) { Quote(text, (string)value); return; }
            if (value is double) { text.Append(((double)value).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (value is int) { text.Append(((int)value).ToString(CultureInfo.InvariantCulture)); return; }
            var array = value as Array;
            if (array != null) { text.Append('['); for (int n = 0; n < array.Length; n++) { if (n > 0) text.Append(','); WriteValue(text, array.GetValue(n)); } text.Append(']'); return; }
            var fields = Fields(value.GetType()).OrderBy(f => f.Name, StringComparer.Ordinal);
            var location = value as ItemLocation;
            text.Append('{'); bool first = true;
            foreach (var f in fields)
            {
                if (location != null && !LocationKeys(location.kind).Contains(f.Name)) continue;
                if (!first) text.Append(','); first = false; Quote(text, f.Name); text.Append(':'); WriteValue(text, f.GetValue(value));
            }
            text.Append('}');
        }
        static void Quote(StringBuilder text, string value)
        {
            text.Append('"');
            foreach (char c in value)
            { if (c == '"' || c == '\\') { text.Append('\\'); text.Append(c); } else if (c < 32 || char.IsSurrogate(c)) { text.Append("\\u"); text.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); } else text.Append(c); }
            text.Append('"');
        }
        sealed class Parser
        {
            static readonly Regex Number = new Regex(@"\G-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);
            readonly string input; int at;
            public Parser(string input) { Need(input != null && input.Length <= 4 * 1024 * 1024); this.input = input; }
            void Space() { while (at < input.Length && " \t\r\n".IndexOf(input[at]) >= 0) at++; }
            bool Take(char c) { Space(); if (at < input.Length && input[at] == c) { at++; return true; } return false; }
            public object Read() { var value = Value(0); Space(); Need(at == input.Length); return value; }
            object Value(int depth)
            {
                Need(depth <= 24); Space(); Need(at < input.Length);
                if (input[at] == '"') return String();
                if (Take('{'))
                {
                    var obj = new Dictionary<string, object>(StringComparer.Ordinal); if (Take('}')) return obj;
                    do { Space(); string key = String(); Need(!obj.ContainsKey(key) && Take(':')); obj.Add(key, Value(depth + 1)); } while (Take(','));
                    Need(Take('}')); return obj;
                }
                if (Take('['))
                { var list = new List<object>(); if (Take(']')) return list; do { Need(list.Count < 4096); list.Add(Value(depth + 1)); } while (Take(',')); Need(Take(']')); return list; }
                if (input.Length - at >= 4 && input.Substring(at, 4) == "null") { at += 4; return null; }
                var match = Number.Match(input, at);
                Need(match.Success); at += match.Length; double n;
                Need(double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out n) && !double.IsInfinity(n) && !double.IsNaN(n)); return n;
            }
            string String()
            {
                Need(at < input.Length && input[at++] == '"'); var value = new StringBuilder();
                while (at < input.Length)
                {
                    char c = input[at++]; if (c == '"') return value.ToString(); Need(c >= 32);
                    if (c != '\\') { value.Append(c); continue; }
                    Need(at < input.Length); c = input[at++];
                    switch (c)
                    {
                        case '"': case '\\': case '/': value.Append(c); break;
                        case 'b': value.Append('\b'); break; case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break; case 'r': value.Append('\r'); break; case 't': value.Append('\t'); break;
                        case 'u':
                            ushort code; Need(at + 4 <= input.Length && Regex.IsMatch(input.Substring(at, 4), "^[0-9a-fA-F]{4}$") && ushort.TryParse(input.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code));
                            value.Append((char)Convert.ToUInt16(input.Substring(at, 4), 16)); at += 4; break;
                        default: throw new InvalidDataException("SCHEMA_INVALID");
                    }
                }
                throw new InvalidDataException("SCHEMA_INVALID");
            }
        }
    }
}
