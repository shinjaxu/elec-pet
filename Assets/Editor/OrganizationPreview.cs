using System.IO;
using ElecPet.Core;
using ElecPet.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElecPet.Editor
{
    public static class OrganizationPreview
    {
        [MenuItem("桌宠/导出整理搬运动画")]
        public static void Render()
        {
            const int width = 800, height = 500;
            var scene = EditorSceneManager.NewPreviewScene();
            var pet = new PrototypePet();
            SceneManager.MoveGameObjectToScene(pet.Root.gameObject, scene);
            var camera = new GameObject("整理预览相机").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene; camera.orthographic = true; camera.orthographicSize = height / 200f;
            camera.transform.position = new Vector3(0, 80, -80); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.91f, .93f, .96f);
            var light = new GameObject("整理预览灯").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(35, -25, 0);
            var previousAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(.65f, .65f, .7f);
            var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            SceneManager.MoveGameObjectToScene(icon, scene); icon.name = "演示图标";
            var material = new Material(Shader.Find("Unlit/Color")) { color = new Color(.25f, .55f, .9f) };
            icon.GetComponent<Renderer>().sharedMaterial = material;
            icon.transform.rotation = camera.transform.rotation;
            icon.transform.localScale = Vector3.one * .32f;
            var texture = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var carry = new DesktopCarry();
            carry.Begin(new DesktopMove("preview", new Point2(420, 230), new Point2(240, 230)),
                new Point2(520, 270), new DesktopRect(88, 88, width - 176, height - 176), new Point2(0, -28));
            Directory.CreateDirectory("docs/previews/organization/frames");
            try
            {
                camera.targetTexture = texture;
                for (int frame = 0; frame < 240; frame++)
                {
                    Vector3 mouth = camera.WorldToScreenPoint(pet.CarryAnchor), root = camera.WorldToScreenPoint(pet.Root.position);
                    carry.SetGripOffset(new Point2(mouth.x - root.x - 16, root.y - mouth.y - 16));
                    var before = World(camera, carry.PetPosition, height);
                    carry.Tick(1.0 / 30, 30, true);
                    var position = World(camera, carry.PetPosition, height);
                    var velocity = (position - before) * 3000;
                    pet.Root.position = position;
                    pet.Animate(Vector2.zero, frame / 30f, velocity.sqrMagnitude > 1, 0,
                        velocity: new Vector2(velocity.x, velocity.z), organizingGrip: (float)carry.Grip, organizingBend: (float)carry.Bend);
                    var point = carry.IconPosition;
                    icon.transform.position = World(camera, new Point2(point.X + 16, point.Y + 16), height) - camera.transform.forward * .02f;
                    camera.Render(); RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    File.WriteAllBytes("docs/previews/organization/frames/" + frame.ToString("D3") + ".png", pixels.EncodeToPNG());
                }
                // 补充四向叼持接触图，避免只验证单一朝向的固定偏移。
                var directions = new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
                for (int direction = 0; direction < directions.Length; direction++)
                {
                    pet.Root.position = Vector3.zero;
                    var worldDirection = World(camera, new Point2(400 + directions[direction].x * 50, 250 + directions[direction].y * 50), height)
                        - World(camera, new Point2(400, 250), height);
                    var velocity = new Vector2(worldDirection.x, worldDirection.z).normalized * 65;
                    for (int warm = 0; warm < 45; warm++)
                        pet.Animate(Vector2.zero, 8 + direction * 1.5f + warm / 30f, true, 0, velocity: velocity, organizingGrip: 1);
                    var mouth = camera.WorldToScreenPoint(pet.CarryAnchor);
                    icon.transform.position = World(camera, new Point2(mouth.x, height - mouth.y), height) - camera.transform.forward * .02f;
                    camera.Render(); RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    File.WriteAllBytes("docs/previews/organization/direction-" + direction + ".png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; RenderSettings.ambientLight = previousAmbient;
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("整理搬运预览已导出，共240帧。");
        }
        private static Vector3 World(Camera camera, Point2 point, int height)
        {
            var ray = camera.ScreenPointToRay(new Vector3((float)point.X, height - (float)point.Y, 0));
            new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance);
            return ray.GetPoint(distance);
        }
    }
}
