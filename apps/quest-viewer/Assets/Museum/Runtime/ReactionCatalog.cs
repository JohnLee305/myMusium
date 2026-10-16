using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FamilyMuseum
{
    // Shared contract with reactions.mjs: all strings use UTF-16 limits and UTF-8 SHA-256.
    public sealed class ReactionCatalog
    {
        public const string Fallback="어떤 이야기가 담겨 있을까요?";
        public static readonly string[] Personas={"general","detail","story","collector"};
        public sealed class Entry { public string Id,Persona,Tier,Text,Status; public string[] Evidence; }
        public sealed class Document { public string ArtifactId,Title,Description,Hash; public bool Enabled; public List<Entry> Entries; }
        readonly LocalArchive archive;
        readonly Dictionary<string,Queue<string>> recent=new Dictionary<string,Queue<string>>();
        readonly Dictionary<string,float> lastUsed=new Dictionary<string,float>();
        readonly System.Random random=new System.Random();
        public ReactionCatalog(string root){archive=new LocalArchive(root);}
        public static string SourceHash(string title,string description)
        {
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(title.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+title+description.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+description))).Replace("-","").ToLowerInvariant();
        }
        static void Check(bool condition){if(!condition)throw new InvalidDataException("REACTION_INVALID");}
        static bool Id(string value)=>value!=null && Regex.IsMatch(value,@"^[a-z0-9][a-z0-9-]{0,63}$");
        static bool Text(string value,int max,bool multiline=false)
        {
            if(string.IsNullOrWhiteSpace(value)||value.Length>max)return false;
            foreach(char c in value)if(c=='<'||c=='>'||c==127||c<32&&!(multiline&&(c=='\n'||c=='\r'||c=='\t')))return false;
            for(int i=0;i<value.Length;i++)if(char.IsHighSurrogate(value[i])){if(i+1>=value.Length||!char.IsLowSurrogate(value[++i]))return false;}else if(char.IsLowSurrogate(value[i]))return false;
            return true;
        }
        static Dictionary<string,object> Object(object value,params string[] keys)
        {
            var obj=value as Dictionary<string,object>;Check(obj!=null && obj.Count==keys.Length && keys.All(obj.ContainsKey));return obj;
        }
        static string Str(object value){Check(value is string);return (string)value;}
        static List<object> Array(object value,int max){var array=value as List<object>;Check(array!=null&&array.Count<=max);return array;}
        public static Document Parse(string json,string artifactId)
        {
            Check(Encoding.UTF8.GetByteCount(json)<=65536);
            var root=Object(new StrictReactionJson(json).Read(),"schemaVersion","artifactId","enabled","source","points","reactions");
            Check(Str(root["schemaVersion"])=="1.0"&&Id(Str(root["artifactId"]))&&Str(root["artifactId"])==artifactId&&root["enabled"] is bool);
            var source=Object(root["source"],"title","description","sha256");
            var doc=new Document{ArtifactId=artifactId,Title=Str(source["title"]),Description=Str(source["description"]),Hash=Str(source["sha256"]),Enabled=(bool)root["enabled"],Entries=new List<Entry>()};
            Check(Text(doc.Title,80,true)&&Text(doc.Description,400,true)&&doc.Hash==SourceHash(doc.Title,doc.Description));
            var points=new HashSet<string>();
            foreach(var value in Array(root["points"],16))
            {
                var p=Object(value,"id","evidence");string id=Str(p["id"]),evidence=Str(p["evidence"]);
                Check(Id(id)&&points.Add(id)&&Text(evidence,400,true)&&(doc.Title.Contains(evidence)||doc.Description.Contains(evidence)));
            }
            var seen=new HashSet<string>();
            foreach(var value in Array(root["reactions"],96))
            {
                var r=Object(value,"id","persona","tier","text","evidencePointIds","status");
                var entry=new Entry{Id=Str(r["id"]),Persona=Str(r["persona"]),Tier=Str(r["tier"]),Text=Str(r["text"]),Status=Str(r["status"]),Evidence=Array(r["evidencePointIds"],16).Select(Str).ToArray()};
                Check(Id(entry.Id)&&seen.Add(entry.Id)&&Id(entry.Persona)&&new[]{"short","normal","rare"}.Contains(entry.Tier)&&Text(entry.Text,72)&&new[]{"draft","approved","rejected"}.Contains(entry.Status)&&entry.Evidence.Distinct().Count()==entry.Evidence.Length&&entry.Evidence.All(points.Contains));
                doc.Entries.Add(entry);
            }
            return doc;
        }
        public Document Load(ArtifactData item)
        {
            try
            {
                if(item==null||!Id(item.id))return null;
                string relative="artifacts/"+item.id+"/reactions.json";
                if(!File.Exists(Path.Combine(archive.Root,relative)))return null;
                string file=archive.Resolve(relative);if(new FileInfo(file).Length>65536)return null;
                var doc=Parse(File.ReadAllText(file).TrimStart('\uFEFF'),item.id);
                return doc.Hash==SourceHash(item.title,item.description)?doc:null;
            }
            catch(Exception){return null;} // Optional content never blocks the gallery.
        }
        public string Select(ArtifactData item,string persona,Queue<string> guestHistory=null)
        {
            var doc=Load(item);if(doc==null||!Personas.Contains(persona))return Fallback;
            if(!doc.Enabled)return "";
            var all=doc.Entries.Where(r=>r.Status=="approved"&&r.Persona==persona).ToList();
            if(!recent.TryGetValue(item.id,out var global))recent[item.id]=global=new Queue<string>();
            if(lastUsed.TryGetValue(item.id,out float usedAt)&&Time.realtimeSinceStartup-usedAt>=30f)global.Clear();
            string key(Entry r)=>item.id+":"+r.Id;
            var eligible=all.Where(r=>!global.Contains(key(r))&&!(guestHistory?.Contains(key(r))??false)).ToList();
            // When all approved candidates were recently used, show nothing for this viewing.
            if(eligible.Count==0)return all.Count==0?Fallback:"";
            var ordinary=eligible.Where(r=>r.Tier!="rare").ToList();
            var rare=eligible.Where(r=>r.Tier=="rare").ToList();
            eligible=rare.Count>0&&random.NextDouble()<.1?rare:ordinary;
            if(eligible.Count==0)return "";
            var selected=eligible[random.Next(eligible.Count)];string selectedKey=key(selected);
            global.Enqueue(selectedKey);while(global.Count>2)global.Dequeue();
            lastUsed[item.id]=Time.realtimeSinceStartup;
            if(guestHistory!=null){guestHistory.Enqueue(selectedKey);while(guestHistory.Count>8)guestHistory.Dequeue();}
            return selected.Text;
        }
        public void Clear(){recent.Clear();lastUsed.Clear();}
    }

    // Reaction mode permits only objects, arrays, strings and booleans. Manifest mode also permits numbers/null. Reject duplicates,
    // unknown properties (above), trailing tokens, non-JSON whitespace and excessive nesting.
    internal sealed class StrictReactionJson
    {
        readonly string input;int at;readonly bool manifest;
        public StrictReactionJson(string value,bool manifest=false){input=value;this.manifest=manifest;}
        void Space(){while(at<input.Length&&" \n\r\t".IndexOf(input[at])>=0)at++;}
        void Need(bool condition){if(!condition)throw new InvalidDataException("REACTION_JSON_INVALID");}
        bool Take(char c){Space();if(at<input.Length&&input[at]==c){at++;return true;}return false;}
        public object Read(){var result=Value(0);Space();Need(at==input.Length);return result;}
        object Value(int depth)
        {
            Need(depth<10);Space();Need(at<input.Length);
            if(input[at]=='"')return String();
            if(Take('{')){var obj=new Dictionary<string,object>();if(Take('}'))return obj;do{Space();string key=String();Need(Take(':')&&!obj.ContainsKey(key));obj.Add(key,Value(depth+1));}while(Take(','));Need(Take('}'));return obj;}
            if(Take('[')){var list=new List<object>();if(Take(']'))return list;do{Need(list.Count<(manifest?10000:96));list.Add(Value(depth+1));}while(Take(','));Need(Take(']'));return list;}
            foreach(var literal in new[]{"true","false"})if(input.Length-at>=literal.Length&&input.Substring(at,literal.Length)==literal){at+=literal.Length;return literal=="true";}
            if(manifest){if(input.Length-at>=4&&input.Substring(at,4)=="null"){at+=4;return null;}var match=Regex.Match(input.Substring(at),@"^-?(?:0|[1-9][0-9]*)(?:.[0-9]+)?(?:[eE][+-]?[0-9]+)?");if(match.Success){at+=match.Length;double number=double.Parse(match.Value,System.Globalization.CultureInfo.InvariantCulture);Need(!double.IsInfinity(number)&&!double.IsNaN(number));return number;}}
            throw new InvalidDataException("REACTION_JSON_TYPE_INVALID");
        }
        string String()
        {
            Need(at<input.Length&&input[at++]=='"');var result=new StringBuilder();
            while(at<input.Length)
            {
                char c=input[at++];if(c=='"')return result.ToString();Need(c>=32);
                if(c!='\\'){result.Append(c);continue;}
                Need(at<input.Length);c=input[at++];
                switch(c){case '"':case '\\':case '/':result.Append(c);break;case 'b':result.Append('\b');break;case 'f':result.Append('\f');break;case 'n':result.Append('\n');break;case 'r':result.Append('\r');break;case 't':result.Append('\t');break;case 'u':Need(at+4<=input.Length&&ushort.TryParse(input.Substring(at,4),System.Globalization.NumberStyles.HexNumber,null,out _));result.Append((char)Convert.ToUInt16(input.Substring(at,4),16));at+=4;break;default:Need(false);break;}
                Need(result.Length<=65536);
            }
            throw new InvalidDataException("REACTION_STRING_UNTERMINATED");
        }
    }
}
