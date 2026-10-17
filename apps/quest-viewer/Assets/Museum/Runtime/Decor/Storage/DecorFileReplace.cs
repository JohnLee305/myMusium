using System;
using System.IO;
using System.Text;

namespace FamilyMuseum.Decor
{
    public enum DecorStoreStage { BeforeWrite, DuringWrite, AfterTempValidated, AfterBackup, BeforeReplace, AfterReplace }
    // No delete-then-move fallback: unsupported atomic replacement is a failed commit.
    internal static class DecorFileReplace
    {
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal static void Save(string target, string text, string backup, Func<string, bool> verify, Action<DecorStoreStage> fault)
        {
            string temp = Path.Combine(Path.GetDirectoryName(target), ".world-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                Hit(fault, DecorStoreStage.BeforeWrite);
                var bytes = Utf8.GetBytes(text);
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    int half = bytes.Length / 2; stream.Write(bytes, 0, half);
                    Hit(fault, DecorStoreStage.DuringWrite);
                    stream.Write(bytes, half, bytes.Length - half); stream.Flush(true);
                }
                if (!verify(File.ReadAllText(temp, Utf8))) throw new InvalidDataException("TEMP_VALIDATION_FAILED");
                Hit(fault, DecorStoreStage.AfterTempValidated);
                if (backup != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    string backupTemp = backup + ".tmp";
                    try
                    {
                        using (var input = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var output = new FileStream(backupTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { input.CopyTo(output); output.Flush(true); }
                        File.Move(backupTemp, backup);
                    }
                    finally { Cleanup(backupTemp); }
                }
                Hit(fault, DecorStoreStage.AfterBackup);
                Hit(fault, DecorStoreStage.BeforeReplace);
                if (File.Exists(target)) File.Replace(temp, target, null);
                else File.Move(temp, target);
                Hit(fault, DecorStoreStage.AfterReplace);
            }
            finally { Cleanup(temp); }
        }
        static void Hit(Action<DecorStoreStage> fault, DecorStoreStage stage) { if (fault != null) fault(stage); }
        static void Cleanup(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
