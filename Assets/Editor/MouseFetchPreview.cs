using System.IO;
using System.Reflection;
using ElecPet.Core;
using ElecPet.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElecPet.Editor
{
    public static class MouseFetchPreview
    {
        [MenuItem("桌宠/导出鼠标捡球预览")]
        public static void Render()
        {
            const int width = 800, height = 500;
            var scene = EditorSceneManager.NewPreviewScene();
            var pet = new PrototypePet();
            SceneManager.MoveGameObjectToScene(pet.Root.gameObject, scene);
            var camera = new GameObject("捡球预览相机").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene; camera.orthographic = true; camera.orthographicSize = height / 200f;
            camera.transform.position = new Vector3(0, 80, -80); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.91f, .93f, .96f);
            var light = new GameObject("捡球预览灯").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(35, -25, 0);
            var host = new GameObject("捡球预览控制器");
            SceneManager.MoveGameObjectToScene(host, scene);
            var controller = host.AddComponent<DesktopPetController>(); controller.enabled = false;
            var motion = new PetMotion(new DesktopRect(0, 0, width, height), 88);
            motion.DragTo(new Point2(300, 230));
            Set(controller, "pet", pet); Set(controller, "petCamera", camera); Set(controller, "motion", motion);
            Set(controller, "behavior", new PetBehavior(new System.Random(1)));
            var texture = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            Directory.CreateDirectory("docs/previews/mouse-fetch/frames");
            try
            {
                camera.targetTexture = texture;
                for (int frame = 0; frame < 225; frame++)
                {
                    Vector2 pointer = frame < 18 ? new Vector2(420, 230)
                        : frame < 24 ? new Vector2(420 + (frame - 17) * 35, 230)
                        : frame < 35 ? new Vector2(630, 230) : new Vector2(260, 230);
                    var before = motion.Position;
                    Call(controller, "TickMouseFetch", pointer, 1f / 30, false);
                    Call(controller, "UpdatePresentation", pointer, before, 1f / 30);
                    camera.Render(); RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    File.WriteAllBytes("docs/previews/mouse-fetch/frames/" + frame.ToString("D3") + ".png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("鼠标捡球预览已导出，共225帧；复用真实互动编排与表现。");
        }
        private static void Set(object value, string field, object data) { value.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, data); }
        private static void Call(object value, string method, params object[] args) { value.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(value, args); }
    }
}
