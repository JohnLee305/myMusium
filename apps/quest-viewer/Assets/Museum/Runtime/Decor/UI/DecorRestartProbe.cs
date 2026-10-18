using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace FamilyMuseum.Decor
{
    public sealed class DecorRestartProbe : MonoBehaviour
    {
        public void Begin(DecorPanel panel, string output) { StartCoroutine(Run(panel, output)); }
        IEnumerator Run(DecorPanel panel, string output)
        {
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return null; Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "world.json"), DecorJson.WriteWorld(panel.Document.world));
            File.WriteAllText(Path.Combine(output, "state.txt"), panel.Session.Persistence.State);
            File.WriteAllText(Path.Combine(output, "message.txt"), panel.Root.Q<Label>("status").text);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, "reopened.png"));
            yield return null; Application.Quit(panel.Session.Persistence.Baseline == null ? 1 : 0);
        }
    }
}
