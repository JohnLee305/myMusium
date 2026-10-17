using System;
using System.IO;
using System.Linq;
using FamilyMuseum.Decor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace FamilyMuseum.Editor
{
    public static class DecorPrototypeSetup
    {
        const string ScenePath = "Assets/Museum/Scenes/DecorPrototype.unity";
        const string UIPath = "Assets/Museum/UI/Decor/";
        public static void CreateAndBuild()
        {
            const string viewingScenePath = "Assets/Museum/Scenes/MuseumEntrance.unity";
            var viewingBytes = File.ReadAllBytes(viewingScenePath);
            var viewingBuildScenes = EditorBuildSettings.scenes.Select(s => s.path + "|" + s.enabled).ToArray();
            AssetDatabase.Refresh();
            var viewingScene = EditorSceneManager.OpenScene(viewingScenePath, OpenSceneMode.Single);
            if (viewingScene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<DecorPanel>(true).Length > 0 || root.GetComponentsInChildren<DecorInputRouter>(true).Length > 0))
                throw new Exception("Viewing scene must not activate decor input");
            var fixture = AssetDatabase.LoadAssetAtPath<TextAsset>(UIPath + "prototype-fixture.json");
            if (!DecorJson.TryRead(fixture.text, out var doc, out var report) || !DecorPlacementValidator.Validate(doc.catalog, doc.world).ok) throw new Exception("Prototype fixture invalid");
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UIPath + "DecorPanelSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>(); settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UIPath + "DecorTheme.tss");
                AssetDatabase.CreateAsset(settings, UIPath + "DecorPanelSettings.asset");
            }
            AssetDatabase.SaveAssets();
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var view = new GameObject("Decor Camera"); var camera = view.AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.fieldOfView = 48; camera.nearClipPlane = .03f; camera.farClipPlane = 160;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.24f, .31f, .27f);
                var rig = view.AddComponent<DecorCamera>();
                var light = new GameObject("Prototype daylight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
                RenderSettings.ambientLight = new Color(.65f, .68f, .62f);
                var root = new GameObject("Decor Prototype"); var ui = root.AddComponent<UIDocument>(); ui.panelSettings = settings;
                ui.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIPath + "DecorPanel.uxml");
                var panel = root.AddComponent<DecorPanel>(); panel.fixture = fixture; panel.cameraRig = rig;
                panel.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "DecorPanel.uss");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            // Reload after scene creation: UI Toolkit's editor initialization may replace the first assignment.
            var authoredScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var authoredPanel = UnityEngine.Object.FindFirstObjectByType<DecorPanel>();
            var authoredUI = authoredPanel.GetComponent<UIDocument>();
            authoredUI.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UIPath + "DecorPanelSettings.asset");
            authoredUI.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIPath + "DecorPanel.uxml");
            authoredPanel.fixture = fixture; authoredPanel.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "DecorPanel.uss");
            if (authoredUI.panelSettings == null) throw new Exception("Prototype panel settings missing");
            EditorUtility.SetDirty(authoredUI); EditorUtility.SetDirty(authoredPanel);
            EditorSceneManager.MarkSceneDirty(authoredScene); EditorSceneManager.SaveScene(authoredScene);
            AssetDatabase.SaveAssets();
            string productName = PlayerSettings.productName;
            try
            {
                // Separate title/PlayerPrefs identity; preserve the museum's authored project settings.
                PlayerSettings.productName = "Museum Decor Prototype";
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64, locationPathName = "Builds/DecorPrototype/DecorPrototype.exe", options = BuildOptions.Development });
                if (result.summary.result != BuildResult.Succeeded) throw new Exception("Decor prototype build failed: " + result.summary.result);
            }
            finally { PlayerSettings.productName = productName; AssetDatabase.SaveAssets(); }
            if (!viewingBytes.SequenceEqual(File.ReadAllBytes(viewingScenePath)) || !viewingBuildScenes.SequenceEqual(EditorBuildSettings.scenes.Select(s => s.path + "|" + s.enabled)))
                throw new Exception("Prototype build modified viewing scene/build list");
            Debug.Log("DECOR_VIEWING_ISOLATION_PASS; viewing scene has no decor input and original scene/build list unchanged");
            Debug.Log("DECOR_PROTOTYPE_BUILD_PASS; existing build scenes preserved");
        }
    }
}
