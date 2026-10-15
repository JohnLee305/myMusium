using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Management;

namespace FamilyMuseum
{
    public sealed class MuseumSession : MonoBehaviour
    {
        public GameObject desktopRig;
        public GameObject xrRig;
        public bool startInVr;
        XRManagerSettings manager;
        bool xrStarted;
        string status = "Desktop preview";
        int errors;

        IEnumerator Start()
        {
            Application.logMessageReceived += TrackErrors;
            string[] args = Environment.GetCommandLineArgs();
            // Automated captures must keep loading even when their window is hidden.
            if (Array.IndexOf(args, "-museum-screenshot") >= 0) Application.runInBackground = true;
            bool vrRequested = startInVr || Array.IndexOf(args, "-museum-vr") >= 0;
#if UNITY_EDITOR
            vrRequested |= UnityEditor.SessionState.GetBool("FamilyMuseum.PlayVR", false);
            UnityEditor.SessionState.SetBool("FamilyMuseum.PlayVR", false);
#endif
            xrRig.SetActive(false);
            desktopRig.SetActive(true);
            var vrInteraction = gameObject.AddComponent<VRExhibitInteraction>();
            var visitors = gameObject.AddComponent<MuseumVisitors>();
            if (vrRequested)
            {
                manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
                if (manager != null)
                {
                    yield return manager.InitializeLoader();
                    if (manager.activeLoader != null)
                    {
                        manager.StartSubsystems();
                        xrStarted = true;
                        desktopRig.SetActive(false);
                        xrRig.SetActive(true);
                        status = "OpenXR initialized; headset verification pending";
                    }
                }
                if (!xrStarted)
                {
                    status = "OpenXR unavailable. Desktop preview is active.";
                    Debug.LogWarning(status);
                }
            }
            Debug.Log("MUSEUM_SESSION_READY: " + status);
            int screenshotArg = Array.IndexOf(args, "-museum-screenshot");
            if (screenshotArg >= 0 && screenshotArg + 1 < args.Length)
            {
                string output = Path.GetFullPath(args[screenshotArg + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
                float deadline = Time.realtimeSinceStartup + 120f;
                GalleryDisplay display;
                // The gallery is created by ArtifactDisplay.Start; component Start order is unspecified.
                while (((display = GetComponent<GalleryDisplay>()) == null || !display.IsFinished) && Time.realtimeSinceStartup < deadline) yield return null;
                bool tourPassed = true;
                if (Array.IndexOf(args, "-museum-tour-smoke") >= 0)
                {
                    tourPassed = display != null && display.IsFinished && display.RoomCount >= 2;
                    if (tourPassed)
                    {
                        foreach (int target in new[] { 1, 0, 1, 0 })
                        {
                            var transition = display.SwitchRoomAsync(target);
                            deadline = Time.realtimeSinceStartup + 120f;
                            while (!transition.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                            int live = FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None).Length - 1;
                            tourPassed &= transition.IsCompleted && !transition.IsFaulted && display.CurrentRoom == target && display.IsFinished &&
                                live == display.ExpectedCount && live <= GalleryDisplay.Capacity && ArtifactDisplay.ActiveImports == display.LoadedCount;
                            Debug.Log($"ROOM_TOUR_CHECK: target={target+1} liveSlots={live} imports={ArtifactDisplay.ActiveImports} passed={tourPassed}");
                        }
                    }
                    Debug.Log(tourPassed ? "ROOM_TOUR_PASS" : "ROOM_TOUR_FAIL");
                }
                bool themePassed = true;
                if (Array.IndexOf(args, "-museum-theme-smoke") >= 0 && display != null && display.IsFinished)
                {
                    var smoke = gameObject.AddComponent<RoomThemeSmoke>();
                    yield return smoke.Run(display);
                    themePassed = smoke.Passed;
                }
                if (Array.IndexOf(args, "-museum-second-room-preview") >= 0 && display != null)
                {
                    var transition = display.SwitchRoomAsync(1);
                    while (!transition.IsCompleted) yield return null;
                    themePassed &= !transition.IsFaulted && display.CurrentRoom == 1;
                }
                bool detailsPassed = true;
                if (Array.IndexOf(args, "-museum-details-smoke") >= 0)
                {
                    var details = GetComponent<ArtifactDetailsPanel>();
                    if (details == null || display == null || !display.IsFinished) detailsPassed = false;
                    else { yield return details.RunSmoke(); detailsPassed = details.SmokePassed; }
                }
                if (Array.IndexOf(args, "-museum-inspection-smoke") >= 0)
                {
                    var details = GetComponent<ArtifactDetailsPanel>();
                    if (details == null || display == null || !display.IsFinished) detailsPassed = false;
                    else { yield return details.RunInspectionSmoke(); detailsPassed &= details.SmokePassed; }
                }
                if (Array.IndexOf(args, "-museum-vr-interaction-smoke") >= 0)
                {
                    if (display == null || !display.IsFinished) detailsPassed = false;
                    else { yield return vrInteraction.RunSmoke(); detailsPassed &= vrInteraction.SmokePassed; }
                }
                if (Array.IndexOf(args, "-museum-visitor-smoke") >= 0)
                {
                    if (display == null || !display.IsFinished) detailsPassed = false;
                    else { yield return visitors.RunSmoke(); detailsPassed &= visitors.SmokePassed; }
                }
                if (Array.IndexOf(args, "-museum-sign-preview") >= 0 && display != null && display.IsFinished) display.PreviewWayfinding();
                yield return new WaitForSecondsRealtime(2f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(output);
                for (int frame = 0; frame < 30; frame++) yield return null;
                bool expectFailure = Array.IndexOf(args, "-museum-expect-artifact-failure") >= 0;
                bool artifactPassed = display != null && display.IsFinished && (expectFailure ? display.FailedCount > 0 : display.FailedCount == 0 && display.LoadedCount == display.ExpectedCount);
                bool passed = File.Exists(output) && errors == 0 && artifactPassed && tourPassed && detailsPassed && themePassed;
                Debug.Log(passed ? "MUSEUM_SMOKE_PASS" : $"MUSEUM_SMOKE_FAIL: errors={errors}, galleryPassed={artifactPassed}, screenshot={File.Exists(output)}");
                Application.Quit(passed ? 0 : 2);
            }
        }

        void TrackErrors(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        void OnGUI()
        {
            if (xrStarted || (GetComponent<ArtifactDetailsPanel>()?.IsOpen ?? false)) return;
            GUI.Box(new Rect(20, Screen.height - 120, 530, 100), "");
            GUI.Label(new Rect(34, Screen.height - 112, 500, 25), "WASD  Move    |    Q / E  Turn left / right    |    Arrow keys  Look");
            GUI.Label(new Rect(34, Screen.height - 85, 500, 25), "Hold right mouse  Look    |    Esc  Release mouse");
            var display = GetComponent<GalleryDisplay>();
            GUI.Label(new Rect(34, Screen.height - 58, 500, 25), display != null ? display.StateLabel : status);
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= TrackErrors;
            if (xrStarted && manager != null)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
        }
    }
}


