using System.IO;
using ElecPet.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElecPet.Editor
{
    public static class PrototypeSetup
    {
        private const string ScenePath = "Assets/Scenes/DesktopPet.unity";

        [MenuItem("桌宠/生成原型场景")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/Scenes");
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Prototype.mat") == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), "Assets/Resources/Prototype.mat");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("桌宠原型").AddComponent<DesktopPetController>();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("原型场景已生成，可以按 Play 预览或构建 Windows 版本。");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "ElecPet";
            PlayerSettings.productName = "ElecPet Prototype";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.forceSingleInstance = true;
            // DWM 透明要求 D3D11 的 BitBlt 交换链，不能使用默认 Flip Model。
            PlayerSettings.useFlipModelSwapchain = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
        }

        [MenuItem("桌宠/构建 Windows x64")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            ConfigurePlayer();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/ElecPet.exe",
                BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("Windows 构建失败，请查看 Unity Console。");
            Debug.Log("Windows 原型构建完成：Builds/Windows/ElecPet.exe");
        }
    }
}
