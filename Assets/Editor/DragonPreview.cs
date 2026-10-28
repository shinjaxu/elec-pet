using System.IO;
using ElecPet.Runtime;
using ElecPet.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElecPet.Editor
{
    public static class DragonPreview
    {
        [MenuItem("桌宠/导出幼龙预览")]
        public static void Render()
        {
            Render(false);
        }

        [MenuItem("桌宠/导出互动姿势预览")]
        public static void RenderInteractions()
        {
            Render(true);
        }

        [MenuItem("桌宠/导出转身步态动态预览")]
        public static void RenderMotion()
        {
            Render(false, true);
        }

        [MenuItem("桌宠/导出模型完整验收")]
        public static void RenderModelReview()
        {
            RenderQuality();
            Render();
            RenderInteractions();
            RenderMotion();
            OrganizationPreview.Render();
        }

        [MenuItem("桌宠/导出模型精修质量预览")]
        public static void RenderQuality()
        {
            const string directory = "docs/previews/quality";
            var scene = EditorSceneManager.NewPreviewScene();
            var previousAmbient = RenderSettings.ambientLight;
            var previousAmbientMode = RenderSettings.ambientMode;
            try
            {
                // 只构造模型和摄影环境，不启动真实桌面 Controller 或整理图标流程。
                var pet = new PrototypePet();
                SceneManager.MoveGameObjectToScene(pet.Root.gameObject, scene);
                var camera = new GameObject("精修预览相机").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.96f, .945f, .91f);
                camera.allowHDR = false;
                var key = new GameObject("精修预览主光").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(key.gameObject, scene);
                key.type = LightType.Directional;
                key.transform.rotation = Quaternion.Euler(35, -25, 0);
                key.intensity = .85f;
                var rim = new GameObject("精修预览轮廓光").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(rim.gameObject, scene);
                rim.type = LightType.Directional;
                rim.transform.rotation = Quaternion.Euler(20, 140, 0);
                rim.color = new Color(.65f, .78f, 1);
                rim.intensity = .65f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .65f, .7f);
                Directory.CreateDirectory(directory);
                float clock = 0;
                pet.Root.localRotation = Quaternion.identity;
                for (int frame = 0; frame < 30; frame++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 0);

                // 大图检查轮廓和接缝；脸部单独对比睁眼与闭眼，避免缩小图掩盖眼睛变形。
                camera.allowMSAA = true;
                camera.orthographicSize = 1.02f;
                var positions = new[] { new Vector3(0, .4f, -5), new Vector3(5, .6f, 0),
                    new Vector3(0, .6f, 5), new Vector3(-2.8f, 1.6f, -5) };
                var names = new[] { "front", "side", "back", "angle" };
                for (int view = 0; view < positions.Length; view++)
                {
                    camera.transform.position = positions[view];
                    camera.transform.LookAt(new Vector3(0, -.05f, .15f));
                    SaveQualityFrame(camera, 1200, 4, directory + "/" + names[view] + ".png");
                }
                Vector3 face = pet.Root.Find("躯干/注视点").position;
                camera.orthographicSize = .42f;
                camera.transform.position = face + new Vector3(0, .12f, -5);
                camera.transform.LookAt(face);
                SaveQualityFrame(camera, 1200, 4, directory + "/face-open.png");
                for (int frame = 132; frame <= 138; frame++) pet.Animate(Vector2.zero, clock = frame / 30f, false, 0);
                SaveQualityFrame(camera, 1200, 4, directory + "/face-blink.png");

                // 与运行相机保持 45 度俯视、每世界单位 100 像素及关闭 MSAA，图片按原始像素直接验收。
                camera.allowMSAA = false;
                camera.orthographicSize = 256 / 200f;
                camera.transform.position = new Vector3(0, 80, -80);
                camera.transform.LookAt(Vector3.zero);
                var backgrounds = new[] { new Color(.96f, .945f, .91f), new Color(.055f, .07f, .095f) };
                var backgroundNames = new[] { "light", "dark" };
                for (int direction = 0; direction < 8; direction++)
                {
                    pet.Root.localRotation = Quaternion.Euler(0, direction * 45, 0);
                    for (int frame = 0; frame < 30; frame++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 0);
                    for (int background = 0; background < backgrounds.Length; background++)
                    {
                        camera.backgroundColor = backgrounds[background];
                        SaveQualityFrame(camera, 256, 1, directory + "/runtime-" + backgroundNames[background] + "-" + (direction * 45) + ".png");
                    }
                }
            }
            finally
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbient;
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("模型精修预览已导出至 docs/previews/quality，包含细节大图与浅深底实际尺寸图。");
        }

        private static void SaveQualityFrame(Camera camera, int size, int samples, string path)
        {
            var target = new RenderTexture(size, size, 24) { antiAliasing = samples };
            var pixels = new Texture2D(size, size, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        private static void Render(bool interactions, bool motion = false)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var pet = new PrototypePet();
            SceneManager.MoveGameObjectToScene(pet.Root.gameObject, scene);
            var camera = new GameObject("预览相机").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = motion ? 1.35f : 1.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.96f, .945f, .91f);
            camera.allowHDR = false;
            var key = new GameObject("预览主光").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(key.gameObject, scene);
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(35, -25, 0);
            key.intensity = .85f;
            var fill = new GameObject("预览补光").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(fill.gameObject, scene);
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(15, 140, 0);
            fill.color = new Color(.65f, .78f, 1);
            fill.intensity = .7f;
            Directory.CreateDirectory("docs/previews");
            if (interactions) Directory.CreateDirectory("docs/previews/interactions");
            if (motion) Directory.CreateDirectory("docs/previews/locomotion-frames");
            int size = motion ? 512 : 900;
            var target = new RenderTexture(size, size, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(size, size, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousAmbient = RenderSettings.ambientLight;
            var previousAmbientMode = RenderSettings.ambientMode;
            try
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .65f, .7f);
                camera.targetTexture = target;
                pet.Animate(new Vector2(.15f, .12f), 1.1f, false, .3f);
                var states = new[] { PetState.Petted, PetState.Guarding, PetState.Startled, PetState.Carried,
                    PetState.Recovering, PetState.Curious, PetState.Stretching, PetState.Grooming, PetState.Drowsy, PetState.Greeting,
                    PetState.Lying, PetState.Alert, PetState.Hunting, PetState.Wander };
                float clock = 1.1f;
                // 预热转向和步态，圆周长度对应六个步周期，使动画首尾连续。
                const float radius = .40743665f;
                if (motion)
                    for (int frame = 0; frame < 60; frame++)
                        pet.Animate(Vector2.left, clock += 1f / 30, true, 0, velocity: Vector2.right * (radius * Mathf.PI * 2 / 8 * 100));
                for (int i = 0; i < (motion ? 240 : interactions ? states.Length : 2); i++)
                {
                    if (motion)
                    {
                        // 沿地面圆形路径实际行走，让动态预览同时展示四足支撑、转身及正侧背面。
                        float angle = i * Mathf.PI * 2 / 240;
                        pet.Root.position = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                        pet.Animate(Vector2.left, clock += 1f / 30, true, 0,
                            velocity: new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * (radius * Mathf.PI * 2 / 8 * 100));
                    }
                    if (interactions)
                        for (int frame = 0; frame < 60; frame++)
                            pet.Animate(new Vector2(.15f, .12f), clock += 1f / 30, states[i] == PetState.Wander,
                                i == 0 ? 1 : 0, states[i], .5f, PetTouch.Tail, true,
                                states[i] == PetState.Wander ? Vector2.right * 65 : Vector2.zero);
                    camera.transform.position = motion ? new Vector3(0, 5, -5)
                        : interactions || i == 1 ? new Vector3(-2.8f, 1.6f, -5) : new Vector3(0, .4f, -5);
                    camera.transform.LookAt(new Vector3(0, -.05f, .15f));
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    pixels.Apply();
                    string path = motion ? "docs/previews/locomotion-frames/" + i.ToString("D3") + ".png"
                        : interactions ? "docs/previews/interactions/" + states[i] + ".png"
                        : "docs/previews/dragon-" + (i == 0 ? "front" : "angle") + ".png";
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbient;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("幼龙预览已导出至 docs/previews。");
        }
    }
}
