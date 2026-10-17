using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FamilyMuseum.Decor
{
    public sealed class DecorBackupInfo { public string id; public int revision; }
    public sealed class DecorLoadResult
    {
        public string status, diagnosticCode;
        public DecorWorld world;
        public DecorBackupInfo[] backups = new DecorBackupInfo[0];
    }
    public sealed class DecorCommitResult
    {
        public string status, commandBatchId, diagnosticCode;
        public int revision;
    }
    public sealed class DecorWorldStore : IDisposable
    {
        readonly string directory, worldId;
        readonly DecorCatalog catalog;
        readonly FileStream writer;
        readonly Action<DecorStoreStage> fault;
        bool disposed, busy;
        string Target { get { return Path.Combine(directory, "world.json"); } }
        DecorWorldStore(string directory, string worldId, DecorCatalog catalog, FileStream writer, Action<DecorStoreStage> fault)
        { this.directory = directory; this.worldId = worldId; this.catalog = DecorCopies.Catalog(catalog); this.writer = writer; this.fault = fault; }
        public static bool TryOpen(string root, string worldId, DecorCatalog catalog, out DecorWorldStore store, out string diagnosticCode)
        { return TryOpen(root, worldId, catalog, null, out store, out diagnosticCode); }
        // fault is opt-in and defaults to null in production. It exists for deterministic I/O and process-death tests.
        public static bool TryOpen(string root, string worldId, DecorCatalog catalog, Action<DecorStoreStage> fault, out DecorWorldStore store, out string diagnosticCode)
        {
            store = null; diagnosticCode = null; FileStream handle = null;
            try
            {
                DecorCopies.Catalog(catalog); string dir = DirectoryFor(root, worldId);
                Directory.CreateDirectory(dir); SafeAncestors(dir);
                string lockFile = Path.Combine(dir, "writer.lock"); SafeFile(lockFile);
                handle = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                store = new DecorWorldStore(dir, worldId, catalog, handle, fault); return true;
            }
            catch (IOException e) { int code = e.HResult & 0xffff; diagnosticCode = code == 32 || code == 33 || code == 11 ? "WORLD_IN_USE" : "STORE_IO_FAILED"; }
            catch (UnauthorizedAccessException) { diagnosticCode = "STORE_ACCESS_DENIED"; }
            catch (ArgumentException) { diagnosticCode = "STORE_PATH_INVALID"; }
            catch (InvalidDataException) { diagnosticCode = "STORE_INPUT_INVALID"; }
            if (handle != null) handle.Dispose(); return false;
        }
        public DecorLoadResult Load()
        { if (disposed) return FailureLoad("failed", "STORE_CLOSED"); return ReadAt(directory, worldId, catalog); }
        public static DecorLoadResult ReadOnly(string root, string worldId, DecorCatalog catalog)
        {
            try { return ReadAt(DirectoryFor(root, worldId), worldId, DecorCopies.Catalog(catalog)); }
            catch (Exception e) when (Expected(e)) { return FailureLoad("failed", "STORE_READ_FAILED"); }
        }
        public DecorCommitResult Commit(int expectedRevision, string commandBatchId, DecorWorld snapshot)
        {
            if (disposed) return Result("failed", "STORE_CLOSED", commandBatchId);
            if (busy) return Result("failed", "STORE_BUSY", commandBatchId);
            if (!Id(commandBatchId) || expectedRevision < 0) return Result("failed", "COMMIT_ARGUMENT_INVALID", commandBatchId);
            busy = true;
            try
            {
                if (!DecorReferenceValidator.Validate(catalog, snapshot).ok || snapshot.id != worldId)
                    return Result("failed", "SNAPSHOT_INVALID", commandBatchId);
                var candidate = DecorCopies.World(snapshot);
                var current = Load();
                if (current.status != "loaded" && current.status != "notFound") return Result("failed", current.diagnosticCode, commandBatchId);
                if (current.status == "notFound" && Directory.Exists(Path.Combine(directory, "backups")) && Backups(directory, worldId, catalog).Length > 0)
                    return Result("failed", "RECOVERY_REQUIRED", commandBatchId);
                if (current.world != null && current.world.lastCommandBatchId == commandBatchId)
                {
                    if (ContentHash(current.world) == ContentHash(candidate)) return Result("alreadySaved", null, commandBatchId, current.world.revision);
                    return Result("conflict", "BATCH_CONTENT_CONFLICT", commandBatchId, current.world.revision);
                }
                int revision = current.world == null ? 0 : current.world.revision;
                if (expectedRevision != revision || candidate.revision != expectedRevision)
                    return Result("conflict", "WORLD_REVISION_CONFLICT", commandBatchId, revision);
                if (revision == int.MaxValue) return Result("failed", "REVISION_EXHAUSTED", commandBatchId, revision);
                candidate.revision = revision + 1; candidate.lastCommandBatchId = commandBatchId;
                string backup = current.world == null ? null : BackupPath(current.world.revision);
                return Save(candidate, commandBatchId, backup);
            }
            catch (Exception e) when (Expected(e)) { return Result("failed", "STORE_IO_FAILED", commandBatchId); }
            finally { busy = false; }
        }
        // Explicit recovery only. minimumRevision can carry the last revision observed by a caller before corruption.
        // With intact store-managed backups, maxBackup+1 bounds the last replacement even after a process dies.
        public DecorCommitResult RestoreBackup(string backupId, int minimumRevision, string commandBatchId)
        {
            if (disposed) return Result("failed", "STORE_CLOSED", commandBatchId);
            if (busy) return Result("failed", "STORE_BUSY", commandBatchId);
            if (!BackupId(backupId) || !Id(commandBatchId) || minimumRevision < 0) return Result("failed", "RECOVERY_ARGUMENT_INVALID", commandBatchId);
            busy = true;
            try
            {
                var current = ReadSingle(Target, worldId, catalog);
                if (current.status == "unsupportedVersion" || current.status == "contentMismatch" || current.status == "failed")
                    return Result("failed", current.diagnosticCode, commandBatchId);
                var source = ReadSingle(Path.Combine(directory, "backups", backupId), worldId, catalog);
                if (source.status != "loaded") return Result("failed", "BACKUP_INVALID", commandBatchId);
                if (current.world != null && current.world.lastCommandBatchId == commandBatchId)
                    return ContentHash(current.world) == ContentHash(source.world) ? Result("alreadySaved", null, commandBatchId, current.world.revision) : Result("conflict", "BATCH_CONTENT_CONFLICT", commandBatchId, current.world.revision);
                var backups = Backups(directory, worldId, catalog);
                long floor = Math.Max(minimumRevision, current.world == null ? 0 : current.world.revision);
                if (backups.Length > 0) floor = Math.Max(floor, (long)backups.Max(b => b.revision) + 1);
                if (floor >= int.MaxValue) return Result("failed", "REVISION_EXHAUSTED", commandBatchId);
                var candidate = DecorCopies.World(source.world); candidate.revision = (int)floor + 1; candidate.lastCommandBatchId = commandBatchId;
                string preserve = current.world != null ? BackupPath(current.world.revision) : File.Exists(Target) ? Path.Combine(directory, "preserved", "corrupt-" + Guid.NewGuid().ToString("N") + ".json") : null;
                return Save(candidate, commandBatchId, preserve, backupId);
            }
            catch (Exception e) when (Expected(e)) { return Result("failed", "RECOVERY_FAILED", commandBatchId); }
            finally { busy = false; }
        }
        DecorCommitResult Save(DecorWorld candidate, string batchId, string backup, string retainBackup = null)
        {
            string expected = DecorJson.WriteWorld(candidate);
            try
            {
                SafeAncestors(directory); SafeFile(Target);
                if (backup != null) { Directory.CreateDirectory(Path.GetDirectoryName(backup)); SafeAncestors(Path.GetDirectoryName(backup)); }
                DecorFileReplace.Save(Target, expected, backup, text =>
                {
                    DecorWorld parsed; ValidationReport shape;
                    return DecorJson.TryReadWorld(text, out parsed, out shape) && DecorReferenceValidator.Validate(catalog, parsed).ok && DecorJson.WriteWorld(parsed) == expected;
                }, fault);
                TrimBackups(retainBackup); return Result("saved", null, batchId, candidate.revision);
            }
            catch (Exception e) when (Expected(e))
            {
                // A post-replace error cannot honestly be called a failed save. Re-read the actual durable snapshot.
                var actual = ReadSingle(Target, worldId, catalog);
                if (actual.world != null && actual.world.lastCommandBatchId == batchId && DecorJson.WriteWorld(actual.world) == expected)
                    return Result("saved", "POST_SAVE_WARNING", batchId, candidate.revision);
                if (actual.status == "failed") return Result("failed", "COMMIT_STATE_UNKNOWN", batchId);
                return Result("failed", "STORE_IO_FAILED", batchId);
            }
        }
        void TrimBackups(string retainBackup)
        {
            var all = Backups(directory, worldId, catalog);
            var keep = all.Take(5).Select(b => b.id).ToList();
            if (retainBackup != null && all.Any(b => b.id == retainBackup) && !keep.Contains(retainBackup)) { keep.RemoveAt(keep.Count - 1); keep.Add(retainBackup); }
            foreach (var old in all.Where(b => !keep.Contains(b.id))) File.Delete(Path.Combine(directory, "backups", old.id));
        }
        string BackupPath(int revision)
        { return Path.Combine(directory, "backups", "r" + revision.ToString("D10", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + ".json"); }
        static DecorLoadResult ReadAt(string directory, string worldId, DecorCatalog catalog)
        {
            try
            {
                SafeAncestors(directory); var current = ReadSingle(Path.Combine(directory, "world.json"), worldId, catalog);
                current.backups = Backups(directory, worldId, catalog);
                if ((current.status == "corrupt" || current.status == "notFound") && current.backups.Length > 0)
                { current.status = "recoveryAvailable"; current.diagnosticCode = "RECOVERY_REQUIRED"; }
                return current;
            }
            catch (Exception e) when (Expected(e)) { return FailureLoad("failed", "STORE_READ_FAILED"); }
        }
        static DecorLoadResult ReadSingle(string path, string worldId, DecorCatalog catalog)
        {
            try
            {
                SafeAncestors(Path.GetDirectoryName(path)); SafeFile(path);
                if (!File.Exists(path)) return FailureLoad("notFound", "WORLD_NOT_FOUND");
                if (new FileInfo(path).Length > 16 * 1024 * 1024) return FailureLoad("corrupt", "WORLD_TOO_LARGE");
                string text = File.ReadAllText(path, DecorFileReplace.Utf8); string version;
                if (DecorJson.TryInspectWorldVersion(text, out version) && version != "decor-slice-1") return FailureLoad("unsupportedVersion", "WORLD_VERSION_UNSUPPORTED");
                DecorWorld world; ValidationReport shape;
                if (!DecorJson.TryReadWorld(text, out world, out shape) || world.id != worldId) return FailureLoad("corrupt", "WORLD_CORRUPT");
                if (world.catalogRevision != catalog.revision) return FailureLoad("contentMismatch", "CATALOG_REVISION_MISMATCH");
                var validation = DecorReferenceValidator.Validate(catalog, world);
                if (!validation.ok) return FailureLoad("contentMismatch", "WORLD_REFERENCES_INVALID");
                return new DecorLoadResult { status = "loaded", world = world };
            }
            catch (System.Text.DecoderFallbackException) { return FailureLoad("corrupt", "WORLD_ENCODING_INVALID"); }
            catch (Exception e) when (Expected(e)) { return FailureLoad("failed", "STORE_READ_FAILED"); }
        }
        static DecorBackupInfo[] Backups(string directory, string worldId, DecorCatalog catalog)
        {
            string dir = Path.Combine(directory, "backups"); SafeAncestors(dir);
            if (!Directory.Exists(dir)) return new DecorBackupInfo[0];
            var result = new List<DecorBackupInfo>();
            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                string name = Path.GetFileName(file); if (!BackupId(name)) continue;
                var read = ReadSingle(file, worldId, catalog);
                if (read.status == "loaded") result.Add(new DecorBackupInfo { id = name, revision = read.world.revision });
            }
            return result.OrderByDescending(b => b.revision).ThenBy(b => b.id, StringComparer.Ordinal).ToArray();
        }
        static string ContentHash(DecorWorld world)
        {
            var copy = DecorCopies.World(world); copy.revision = 0; copy.lastCommandBatchId = null;
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(DecorFileReplace.Utf8.GetBytes(DecorJson.WriteWorld(copy))));
        }
        static bool Id(string id) { return id != null && Regex.IsMatch(id, @"\A[a-z][a-z0-9.-]{0,63}\z"); }
        static bool BackupId(string id) { return id != null && Regex.IsMatch(id, @"\Ar[0-9]{10}-[a-f0-9]{32}\.json\z"); }
        static string DirectoryFor(string root, string worldId)
        { if (string.IsNullOrWhiteSpace(root) || !Id(worldId)) throw new ArgumentException("STORE_PATH_INVALID"); var dir = Path.GetFullPath(Path.Combine(Path.GetFullPath(root), worldId)); SafeAncestors(dir); return dir; }
        static void SafeAncestors(string dir)
        { for (var info = new DirectoryInfo(dir); info != null; info = info.Parent) if (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("STORE_LINK_NOT_ALLOWED"); }
        static void SafeFile(string path) { if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("STORE_LINK_NOT_ALLOWED"); }
        static DecorLoadResult FailureLoad(string status, string code) { return new DecorLoadResult { status = status, diagnosticCode = code }; }
        static DecorCommitResult Result(string status, string code, string batch, int revision = 0) { return new DecorCommitResult { status = status, diagnosticCode = code, commandBatchId = batch, revision = revision }; }
        static bool Expected(Exception e) { return e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidDataException; }
        public void Dispose() { if (!disposed) { disposed = true; writer.Dispose(); } }
    }
}
