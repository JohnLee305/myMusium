using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Unity.XR.CoreUtils;

namespace FamilyMuseum.Editor
{
    public static class MuseumSetup
    {
        const string ScenePath = "Assets/Museum/Scenes/MuseumEntrance.unity";

        public static void ConfigureAndCreate()
        {
            Directory.CreateDirectory("Assets/Museum/Scenes");
            Directory.CreateDirectory("Assets/Museum/Materials");
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.Refresh();
            ConfigureXR();
            PlayerSettings.companyName = "Family Museum";
            PlayerSettings.productName = "VR Museum";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();

            // Existing authored scenes are preserved. Initial generation only.
            if (!File.Exists(ScenePath)) CreateScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Verify();
        }

        static void ConfigureXR()
        {
            const string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, settingsPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (general.Manager == null)
            {
                general.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                general.Manager.name = "Standalone XR Manager";
                AssetDatabase.AddObjectToAsset(general.Manager, perTarget);
            }
            general.InitManagerOnStart = false;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Standalone))
                throw new Exception("OpenXR loader assignment failed");
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            var touch = openxr.GetFeature<OculusTouchControllerProfile>();
            if (touch == null) throw new Exception("Oculus Touch interaction profile not available");
            touch.enabled = true;
            EditorUtility.SetDirty(openxr);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(perTarget);
        }

        static Material Material(string name, Color color, float metallic = 0f)
        {
            string path = "Assets/Museum/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", 0.25f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        static void Label(string name, string text, Vector3 position, float size, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.position = position;
            var mesh = obj.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 100;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            obj.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        static Camera CameraOn(GameObject parent, string name, Vector3 localPosition)
        {
            var obj = new GameObject(name);
            obj.tag = "MainCamera";
            obj.transform.SetParent(parent.transform, false);
            obj.transform.localPosition = localPosition;
            var camera = obj.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 60f;
            camera.fieldOfView = 65f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.15f, 0.17f);
            obj.AddComponent<AudioListener>();
            return camera;
        }

        static void Track(GameObject obj, string device, bool hmd)
        {
            var pose = obj.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, device + (hmd ? "/centerEyePosition" : "/devicePosition")));
            pose.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, device + (hmd ? "/centerEyeRotation" : "/deviceRotation")));
            pose.trackingStateInput = new InputActionProperty(new InputAction("Tracking", InputActionType.Value, device + "/trackingState"));
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var wall = Material("Warm plaster", new Color(0.77f, 0.75f, 0.68f));
            var floor = Material("Slate floor", new Color(0.18f, 0.23f, 0.25f));
            var stone = Material("Display stone", new Color(0.61f, 0.62f, 0.57f));
            var brass = Material("Brass", new Color(0.64f, 0.36f, 0.14f), 0.35f);
            var dark = new Color(0.12f, 0.19f, 0.21f);
            Box("Floor", new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 10), floor);
            Box("Back wall", new Vector3(0, 2f, 4f), new Vector3(10, 4f, 0.2f), wall);
            Box("Left wall", new Vector3(-5f, 2f, 0), new Vector3(0.2f, 4f, 10), wall);
            Box("Right wall", new Vector3(5f, 2f, 0), new Vector3(0.2f, 4f, 10), wall);
            Box("Front wall", new Vector3(0, 2f, -5f), new Vector3(10, 4f, 0.2f), wall);
            Box("Wall accent", new Vector3(0, 2.63f, 3.87f), new Vector3(3.4f, 0.018f, 0.025f), brass);
            Label("Museum title", "F A M I L Y   A R C H I V E", new Vector3(0, 3.14f, 3.85f), 0.042f, dark);
            Label("Museum subtitle", "A place for the things we keep.", new Vector3(0, 2.85f, 3.85f), 0.018f, dark);
            Box("Display plinth", new Vector3(0, 0.5f, 0.8f), new Vector3(1f, 1f, 1f), stone);
            Box("Display cap", new Vector3(0, 1.015f, 0.8f), new Vector3(1.06f, 0.03f, 1.06f), brass);
            var sample = Box("Scale reference - 30 cm", new Vector3(0, 1.25f, 0.8f), Vector3.one * 0.3f, brass);
            sample.transform.rotation = Quaternion.Euler(12, 25, 8);
            Label("Sample label", "01 / SCALE STUDY", new Vector3(0, 0.76f, 0.285f), 0.009f, dark);
            Label("Sample description", "30 cm reference cube\nFirst gallery / environment preview", new Vector3(0, 0.58f, 0.285f), 0.0055f, dark);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.60f);
            var sun = new GameObject("Gallery light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.transform.rotation = Quaternion.Euler(40f, -25f, 0);
            sun.shadows = LightShadows.Soft;

            var desktop = new GameObject("Desktop visitor");
            desktop.transform.position = new Vector3(0, 0.05f, -2.8f);
            var character = desktop.AddComponent<CharacterController>();
            character.height = 1.7f;
            character.center = new Vector3(0, 0.85f, 0);
            character.radius = 0.2f;
            character.minMoveDistance = 0;
            var desktopCamera = CameraOn(desktop, "Desktop camera", new Vector3(0, 1.65f, 0));
            desktop.AddComponent<DesktopNavigator>().view = desktopCamera.transform;

            var xr = new GameObject("XR visitor");
            xr.transform.position = new Vector3(0, 0, -2.8f);
            var origin = xr.AddComponent<XROrigin>();
            var offset = new GameObject("Camera offset");
            offset.transform.SetParent(xr.transform, false);
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 1.65f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.Camera = CameraOn(offset, "XR camera", Vector3.zero);
            Track(origin.Camera.gameObject, "<XRHMD>", true);
            foreach (string hand in new[] { "LeftHand", "RightHand" })
            {
                var controller = new GameObject(hand);
                controller.transform.SetParent(offset.transform, false);
                Track(controller, "<XRController>{" + hand + "}", false);
                var marker = Box(hand + " marker", Vector3.zero, new Vector3(0.04f, 0.04f, 0.1f), brass);
                marker.transform.SetParent(controller.transform, false);
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            }
            xr.SetActive(false);
            var session = new GameObject("Museum session").AddComponent<MuseumSession>();
            session.desktopRig = desktop;
            session.xrRig = xr;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        public static void UpdatePreviewTypography()
        {
            EditorSceneManager.OpenScene(ScenePath);
            GameObject.Find("Museum title").GetComponent<TextMesh>().characterSize = 0.042f;
            GameObject.Find("Museum subtitle").GetComponent<TextMesh>().characterSize = 0.018f;
            GameObject.Find("Sample label").GetComponent<TextMesh>().characterSize = 0.009f;
            GameObject.Find("Sample description").GetComponent<TextMesh>().characterSize = 0.0055f;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            BuildDesktop();
        }

        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var session = UnityEngine.Object.FindFirstObjectByType<MuseumSession>();
            if (session == null || session.desktopRig == null || session.xrRig == null) throw new Exception("Visitor rigs are missing");
            if (session.xrRig.activeSelf || !session.desktopRig.activeSelf) throw new Exception("Default desktop mode is incorrect");
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (general == null || general.InitManagerOnStart || !general.Manager.activeLoaders.Any(loader => loader is OpenXRLoader))
                throw new Exception("Manual OpenXR setup is incorrect");
            if (!OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone).GetFeature<OculusTouchControllerProfile>().enabled)
                throw new Exception("Oculus Touch profile must be enabled");
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                        throw new Exception("Missing script: " + transform.name);
            Debug.Log("MUSEUM_CONFIGURATION_PASS");
        }

        public static void ConfigureArchive()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var session = UnityEngine.Object.FindFirstObjectByType<MuseumSession>();
            var display = session.GetComponent<ArtifactDisplay>();
            if (display == null) display = session.gameObject.AddComponent<ArtifactDisplay>();
            display.placeholder = GameObject.Find("Scale reference - 30 cm");
            display.titleLabel = GameObject.Find("Sample label").GetComponent<TextMesh>();
            display.descriptionLabel = GameObject.Find("Sample description").GetComponent<TextMesh>();
            var slot = GameObject.Find("Archive model slot") ?? new GameObject("Archive model slot");
            slot.transform.position = new Vector3(0, 1.04f, 0.8f);
            display.modelSlot = slot.transform;
            // Runtime shader discovery requires inclusion in the standalone player.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (string shaderName in new[] { "glTF/PbrMetallicRoughness", "glTF/PbrSpecularGlossiness", "glTF/Unlit" })
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) throw new Exception("Missing glTF shader: " + shaderName);
                bool exists = false;
                for (int i = 0; i < shaders.arraySize; i++) exists |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (!exists) { int index = shaders.arraySize; shaders.InsertArrayElementAtIndex(index); shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader; }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Verify();
        }

        public static void BuildDesktop()
        {
            Verify();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = Path.Combine(root, "Builds/Windows/VRMuseum.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("MUSEUM_BUILD_PASS: " + report.summary.outputPath);
        }

        [MenuItem("Museum/Play Desktop Preview")]
        static void PlayDesktop() { UnityEditor.SessionState.SetBool("FamilyMuseum.PlayVR", false); OpenAndPlay(); }
        [MenuItem("Museum/Play VR (headset required)")]
        static void PlayVR() { UnityEditor.SessionState.SetBool("FamilyMuseum.PlayVR", true); OpenAndPlay(); }
        static void OpenAndPlay()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
