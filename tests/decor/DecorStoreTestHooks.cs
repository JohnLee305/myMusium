using System;
using System.IO;
using System.Threading;
using FamilyMuseum.Decor;

// Test-only hooks: never compiled into the Unity project.
public static class DecorStoreTestHooks
{
    public static Action<DecorStoreStage> FailAt(DecorStoreStage requested)
    { return stage => { if (stage == requested) throw new IOException("injected I/O failure at " + stage); }; }
    public static Action<DecorStoreStage> StopAt(DecorStoreStage requested, string marker)
    {
        return stage => { if (stage == requested) { File.WriteAllText(marker, stage.ToString()); Thread.Sleep(Timeout.Infinite); } };
    }
}
