using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FamilyMuseum
{
    [Serializable] public sealed class MuseumManifest { public string schemaVersion; public string[] artifacts; public MuseumRoom[] rooms; }
    [Serializable] public sealed class MuseumRoom { public string id; public string title; public string[] artifacts; public int[] cells; public RoomTheme theme; }
    [Serializable] public sealed class ArtifactData
    {
        public string schemaVersion;
        public string id;
        public string title;
        public string description;
        public string model;
        public float scale;
    }
    public sealed class LocalArchive
    {
        public string Root { get; }
        public LocalArchive(string root) { Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        public static string FindRoot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-museum-archive");
            if (index >= 0)
            {
                if (index + 1 >= args.Length) throw new InvalidDataException("ARCHIVE_ARGUMENT_MISSING");
                return Path.GetFullPath(args[index + 1]);
            }
            var directory = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 5 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "museum-archive");
                if (Directory.Exists(candidate)) return candidate;
            }
            throw new DirectoryNotFoundException("ARCHIVE_NOT_FOUND: use -museum-archive <folder>");
        }
        public string Resolve(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("\\") || relative.Contains(":")) throw new InvalidDataException("UNSAFE_PATH");
            string part = Root;
            foreach (string segment in relative.Split('/'))
            {
                if (segment == "" || segment == "." || segment == "..") throw new InvalidDataException("UNSAFE_PATH");
                part = Path.Combine(part, segment);
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("LINK_NOT_ALLOWED");
            }
            string full = Path.GetFullPath(part);
            if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("OUTSIDE_ARCHIVE");
            if (!File.Exists(full)) throw new FileNotFoundException("FILE_NOT_FOUND", relative);
            return full;
        }
        T Read<T>(string relative)
        {
            string file = Resolve(relative);
            if (new FileInfo(file).Length > (relative == "manifest.json" ? 4 : 1) * 1024 * 1024) throw new InvalidDataException("JSON_TOO_LARGE");
            return JsonUtility.FromJson<T>(File.ReadAllText(file));
        }
        public MuseumRoom[] ReadRooms()
        {
            var manifest = Read<MuseumManifest>("manifest.json");
            if (manifest == null || manifest.schemaVersion != "1.0" || manifest.artifacts == null || manifest.artifacts.Length > 10000) throw new InvalidDataException("MANIFEST_INVALID: expected 0..10000 artifacts");
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (string path in manifest.artifacts)
                if (path == null || !Regex.IsMatch(path, @"^artifacts/[a-z0-9][a-z0-9-]{0,63}/artifact\.json$") || !seen.Add(path)) throw new InvalidDataException("MANIFEST_PATH_OR_DUPLICATE_INVALID");
            if (manifest.rooms == null)
            {
                var rooms = new System.Collections.Generic.List<MuseumRoom>();
                for (int i = 0; i < Math.Max(1, manifest.artifacts.Length); i += 30)
                {
                    var entries = new string[Math.Min(30, manifest.artifacts.Length - i)];
                    Array.Copy(manifest.artifacts, i, entries, 0, entries.Length);
                    rooms.Add(new MuseumRoom { id = "room-" + (rooms.Count + 1), title = (rooms.Count + 1) + "번 전시실", artifacts = entries });
                }
                foreach (var room in rooms) ValidateCells(room);
                return rooms.ToArray();
            }
            if (manifest.rooms.Length < 1 || manifest.rooms.Length > 1000) throw new InvalidDataException("ROOM_COUNT_INVALID");
            var roomIds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var assigned = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var room in manifest.rooms)
            {
                if (room == null || room.id == null || !Regex.IsMatch(room.id, @"^[a-z0-9][a-z0-9-]{0,63}$") || !roomIds.Add(room.id) ||
                    string.IsNullOrWhiteSpace(room.title) || room.title.Length > 80 || room.artifacts == null || room.artifacts.Length > 30) throw new InvalidDataException("ROOM_INVALID");
                room.theme?.Validate();
                ValidateCells(room);
                foreach (string entry in room.artifacts)
                    if (entry == null || !seen.Contains(entry) || !assigned.Add(entry)) throw new InvalidDataException("ROOM_MEMBERSHIP_INVALID");
            }
            if (assigned.Count != seen.Count) throw new InvalidDataException("ROOM_UNASSIGNED_ARTIFACT");
            return manifest.rooms;
        }
        static void ValidateCells(MuseumRoom room)
        {
            if (room.cells == null) { room.cells = new int[room.artifacts.Length]; for (int i=0;i<room.cells.Length;i++) room.cells[i]=i; }
            if (room.cells.Length != room.artifacts.Length) throw new InvalidDataException("ROOM_CELLS_INVALID");
            var occupied = new System.Collections.Generic.HashSet<int>();
            foreach (int cell in room.cells) if (cell<0 || cell>=30 || !occupied.Add(cell)) throw new InvalidDataException("ROOM_CELLS_INVALID");
        }
        public ArtifactData ReadArtifact(string relative)
        {
            if (relative == null || !Regex.IsMatch(relative, @"^artifacts/[a-z0-9][a-z0-9-]{0,63}/artifact\.json$")) throw new InvalidDataException("ARTIFACT_PATH_INVALID");
            var data = Read<ArtifactData>(relative);
            if (data == null || data.schemaVersion != "1.0" || data.id == null || !Regex.IsMatch(data.id, @"^[a-z0-9][a-z0-9-]{0,63}$") ||
                string.IsNullOrWhiteSpace(data.title) || data.title.Length > 80 || string.IsNullOrWhiteSpace(data.description) || data.description.Length > 400 ||
                float.IsNaN(data.scale) || float.IsInfinity(data.scale) || data.scale < 0.01f || data.scale > 10f) throw new InvalidDataException("ARTIFACT_INVALID");
            if (relative != $"artifacts/{data.id}/artifact.json" || data.model == null ||
                !Regex.IsMatch(data.model, @"^artifacts/" + Regex.Escape(data.id) + @"/models/[a-zA-Z0-9_-]+\.glb$")) throw new InvalidDataException("ARTIFACT_ID_PATH_MISMATCH");
            return data;
        }
        [Serializable] sealed class GlbRoot { public GlbAsset asset; public GlbUri[] buffers; public GlbUri[] images; }
        [Serializable] sealed class GlbAsset { public string version; }
        [Serializable] sealed class GlbUri { public string uri; }
        public byte[] ReadGlb(string relative)
        {
            string file = Resolve(relative);
            long length = new FileInfo(file).Length;
            if (length < 20 || length > 64 * 1024 * 1024) throw new InvalidDataException("GLB_SIZE_INVALID");
            byte[] bytes = File.ReadAllBytes(file);
            if (Encoding.ASCII.GetString(bytes, 0, 4) != "glTF" || BitConverter.ToUInt32(bytes, 4) != 2 || BitConverter.ToUInt32(bytes, 8) != bytes.Length) throw new InvalidDataException("GLB_HEADER_INVALID");
            uint jsonLength = BitConverter.ToUInt32(bytes, 12);
            if (jsonLength > bytes.Length - 20 || BitConverter.ToUInt32(bytes, 16) != 0x4e4f534a) throw new InvalidDataException("GLB_JSON_CHUNK_INVALID");
            var json = JsonUtility.FromJson<GlbRoot>(Encoding.UTF8.GetString(bytes, 20, (int)jsonLength));
            if (json?.asset?.version != "2.0") throw new InvalidDataException("GLTF_VERSION_INVALID");
            CheckUris(json.buffers); CheckUris(json.images);
            return bytes;
        }
        static void CheckUris(GlbUri[] items)
        {
            if (items == null) return;
            foreach (var item in items)
                if (!string.IsNullOrEmpty(item?.uri) && !item.uri.StartsWith("data:", StringComparison.Ordinal)) throw new InvalidDataException("GLB_EXTERNAL_RESOURCE_NOT_ALLOWED");
        }
    }
}
