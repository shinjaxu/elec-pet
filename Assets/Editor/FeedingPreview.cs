using System.IO;
using ElecPet.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElecPet.Editor
{
    public static class FeedingPreview
    {
        [MenuItem("桌宠/导出文件喂食预览")]
        public static void Render()
        {
            const string directory = "docs/previews/feeding";
            const int width = 640, height = 480;
            var scene = EditorSceneManager.NewPreviewScene();
            var previousAmbient = RenderSettings.ambientLight;
            var previousMode = RenderSettings.ambientMode;
            var previousTarget = RenderTexture.active;
            var visual = new PetFoodVisual();
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            Camera camera = null;
            try
            {
                // 预览直接调用运行时模型与代理，不启动 Controller、文件选择或回收站操作。
                var pet = new PrototypePet();
                SceneManager.MoveGameObjectToScene(pet.Root.gameObject, scene);
                pet.Root.localRotation = Quaternion.Euler(0, -15, 0);
                camera = new GameObject("喂食预览相机").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene; camera.orthographic = true; camera.orthographicSize = .72f;
                camera.transform.position = new Vector3(0, 1.6f, -5);
                camera.transform.LookAt(new Vector3(0, -.04f, .13f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.94f, .955f, .94f);
                camera.allowHDR = false; camera.targetTexture = target;
                var key = new GameObject("喂食预览主光").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(key.gameObject, scene);
                key.type = LightType.Directional; key.intensity = .85f;
                key.transform.rotation = Quaternion.Euler(35, -25, 0);
                var rim = new GameObject("喂食预览轮廓光").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(rim.gameObject, scene);
                rim.type = LightType.Directional; rim.intensity = .65f;
                rim.color = new Color(.65f, .78f, 1); rim.transform.rotation = Quaternion.Euler(20, 140, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .65f, .7f);
                Directory.CreateDirectory(directory + "/frames");
                Directory.CreateDirectory(directory + "/failed-frames");
                float clock = 0;
                for (int frame = 0; frame < 30; frame++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 0);
                foreach (bool failed in new[] { false, true })
                for (int frame = 0; frame <= 150; frame++)
                {
                    float progress = frame / 150f;
                    pet.Animate(Vector2.zero, clock += 1f / 30, false, 0, feedingProgress: progress, feedingFailed: failed);
                    visual.Present(pet, camera, progress, failed ? ".mp4" : ".png", failed);
                    Save(camera, target, pixels, directory + (failed ? "/failed-frames/" : "/frames/") + frame.ToString("D3") + ".png");
                    if (!failed && (frame == 12 || frame == 37 || frame == 75 || frame == 111 || frame == 132))
                        Save(camera, target, pixels, directory + "/" + (frame == 12 ? "01-sniff" : frame == 37 ? "02-bite"
                            : frame == 75 ? "03-chew" : frame == 111 ? "04-swallow" : "05-satisfied") + ".png");
                    if (failed && frame == 129) Save(camera, target, pixels, directory + "/06-failed.png");
                }
                // 实际桌面每世界单位 100 像素，另存原尺寸关键帧，避免只凭放大图验收。
                camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                target = new RenderTexture(256, 256, 24);
                pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
                camera.targetTexture = target; camera.orthographicSize = 256 / 200f;
                camera.transform.position = new Vector3(0, 80, -80); camera.transform.LookAt(Vector3.zero);
                for (int frame = 0; frame < 30; frame++)
                    pet.Animate(Vector2.zero, clock += 1f / 30, false, 0, feedingProgress: .25f);
                visual.Present(pet, camera, .25f, ".txt", false);
                Save(camera, target, pixels, directory + "/runtime-bite.png");
            }
            finally
            {
                visual.Dispose();
                if (camera != null) camera.targetTexture = null;
                RenderTexture.active = previousTarget;
                RenderSettings.ambientLight = previousAmbient; RenderSettings.ambientMode = previousMode;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("文件喂食预览已导出至 docs/previews/feeding，成功与失败分别包含151帧，未访问用户文件。");
        }

        private static void Save(Camera camera, RenderTexture target, Texture2D pixels, string path)
        {
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
    }
}
