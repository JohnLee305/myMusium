using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FamilyMuseum
{
    [Serializable] public sealed class VisitorAppearancePreference
    {
        public string id;
        public bool enabled;
        public float weight=-1;
    }
    [Serializable] public sealed class VisitorPreferences
    {
        public const int MaxCount=12;
        public string schemaVersion;
        public bool enabled;
        public int count=-1;
        public string mode;
        public float speed=-1;
        public VisitorAppearancePreference[] appearances;
        public static VisitorPreferences Default()=>new VisitorPreferences {schemaVersion="1.0",enabled=true,count=1,speed=1,appearances=Array.Empty<VisitorAppearancePreference>()};
        public void Validate()
        {
            if(mode!=null && mode!="" && mode!="manual" && mode!="auto") throw new InvalidDataException("VISITOR_MODE_INVALID");
            if(schemaVersion!="1.0" || count<0 || count>MaxCount || float.IsNaN(speed) || float.IsInfinity(speed) || speed<.5f || speed>1.5f || appearances==null || appearances.Length>256) throw new InvalidDataException("VISITOR_SETTINGS_INVALID");
            var seen=new HashSet<string>();
            foreach(var entry in appearances)
                if(entry==null || entry.id==null || !Regex.IsMatch(entry.id,@"^[a-z0-9][a-z0-9-]{0,63}$") || !seen.Add(entry.id) || float.IsNaN(entry.weight) || float.IsInfinity(entry.weight) || entry.weight<0 || entry.weight>100) throw new InvalidDataException("VISITOR_APPEARANCE_SETTINGS_INVALID");
        }
        public VisitorAppearancePreference Find(string id)=>Array.Find(appearances,entry=>entry.id==id);
    }
    public sealed class VisitorPreferencesFile
    {
        readonly LocalArchive archive;
        public VisitorPreferencesFile(string root) { archive=new LocalArchive(root); }
        public string FilePath=>Path.Combine(archive.Root,"visitors.json");
        public bool TryRead(out VisitorPreferences settings,out string fingerprint,out string error)
        {
            settings=null; fingerprint=null; error=null;
            try
            {
                if(!File.Exists(FilePath)) {settings=VisitorPreferences.Default();fingerprint="missing";return true;}
                string file=archive.Resolve("visitors.json");
                if(new FileInfo(file).Length>64*1024) throw new InvalidDataException("VISITOR_SETTINGS_TOO_LARGE");
                string text=File.ReadAllText(file).TrimStart('\uFEFF');
                fingerprint=text+"|"+File.GetLastWriteTimeUtc(file).Ticks;
                settings=JsonUtility.FromJson<VisitorPreferences>(text);
                if(settings==null) throw new InvalidDataException("VISITOR_SETTINGS_EMPTY");
                settings.Validate(); return true;
            }
            catch(Exception exception) {error=exception.Message;settings=null;return false;}
        }
    }
}
