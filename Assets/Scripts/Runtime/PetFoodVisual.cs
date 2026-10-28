using System;
using UnityEngine;

namespace ElecPet.Runtime
{
    // 文件只用扩展名选择代理图案；视觉层从不读取、移动或删除用户文件。
    public sealed class PetFoodVisual : IDisposable
    {
        private Transform root, card;
        private readonly Transform[] sparks = new Transform[9];
        private Material paper, sparkle;
        private Texture2D texture;
        private Mesh quad;
        private int kind = -1;

        public void Present(PrototypePet pet, Camera camera, float progress, string extension, bool failed)
        {
            bool visible = progress >= 0 && progress <= 1;
            if (!visible) { if (root != null) root.gameObject.SetActive(false); return; }
            if (root == null)
            {
                Create();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, pet.Root.gameObject.scene);
            }
            root.gameObject.SetActive(true);
            int nextKind = Kind(extension);
            if (kind != nextKind)
            {
                Destroy(texture);
                texture = DrawCard(nextKind); paper.mainTexture = texture; kind = nextKind;
            }

            float entering = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .32f, progress));
            float retreat = failed ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.67f, .86f, progress)) : 0;
            Vector3 forward = pet.Root.TransformDirection(Vector3.back);
            // 代理沿嘴部朝向靠近；进入口腔后才收缩，失败时沿原方向退回。
            root.position = pet.CarryAnchor + forward * (.30f * (1 - entering) + .30f * retreat)
                + Vector3.up * (Mathf.Sin(progress * Mathf.PI * 2) * .018f * (1 - entering));
            root.rotation = camera.transform.rotation;
            card.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(progress * Mathf.PI * 4) * 7 * (1 - entering) - retreat * 12);
            float consumed = failed ? entering * (1 - retreat) : entering;
            float scale = Mathf.Lerp(.28f, .035f, consumed);
            card.localScale = new Vector3(scale * .8f, scale, 1);
            card.gameObject.SetActive(progress < .335f || (failed && progress >= .67f));

            float burst = !failed && progress >= .78f ? Mathf.Clamp01((progress - .78f) / .20f) : -1;
            for (int i = 0; i < sparks.Length; i++)
            {
                bool active = burst >= 0 && burst < 1;
                sparks[i].gameObject.SetActive(active);
                if (!active) continue;
                float angle = i * 2.399963f;
                float radius = .04f + burst * (.12f + i % 3 * .025f);
                sparks[i].localPosition = new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius + burst * .10f, -.02f);
                sparks[i].localRotation = Quaternion.Euler(0, 0, 45 + burst * 90 + i * 25);
                sparks[i].localScale = Vector3.one * ((i % 3 == 0 ? .024f : .014f) * Mathf.Sin(burst * Mathf.PI));
            }
        }

        private void Create()
        {
            root = new GameObject("喂食食物代理").transform;
            quad = new Mesh { name = "喂食卡片平面" };
            quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0),
                new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.RecalculateNormals(); quad.RecalculateBounds();
            // 内置精灵着色器已在 GraphicsSettings 保留，程序化透明卡片无需新增构建材质变体。
            paper = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            sparkle = new Material(paper) { color = new Color(.57f, .91f, .78f) };
            card = Piece("食物卡片", paper);
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Piece("满足星点", sparkle);
        }

        private Transform Piece(string name, Material material)
        {
            var part = new GameObject(name).transform;
            part.SetParent(root, false);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part;
        }

        private static int Kind(string extension)
        {
            string value = (extension ?? "").ToLowerInvariant();
            if (value == ".png" || value == ".jpg" || value == ".jpeg" || value == ".gif" || value == ".webp"
                || value == ".bmp" || value == ".tiff" || value == ".heic") return 1;
            if (value == ".mp4" || value == ".mov" || value == ".avi" || value == ".mkv" || value == ".webm"
                || value == ".wmv" || value == ".m4v") return 2;
            return 0;
        }

        private static Texture2D DrawCard(int type)
        {
            const int size = 96;
            var pixels = new Color[size * size];
            var accent = type == 1 ? new Color(.25f, .64f, .50f) : type == 2 ? new Color(.48f, .40f, .75f) : new Color(.25f, .48f, .75f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 圆角纸卡与折角仅由固定图案生成，三种文件类型在小尺寸下也可区分。
                float dx = Mathf.Max(7 - x, x - 88), dy = Mathf.Max(7 - y, y - 88);
                if (x < 3 || x > 92 || y < 3 || y > 92 || (dx > 0 && dy > 0 && dx * dx + dy * dy > 36)) continue;
                Color color = new Color(.94f, .97f, .99f);
                if (y > 75) color = accent;
                if (x > 70 && y > 73 && x + y > 159) color = Color.Lerp(accent, Color.white, .50f);
                if (type == 0 && x > 20 && x < (y > 38 ? 74 : 57)
                    && ((y > 50 && y < 56) || (y > 36 && y < 42) || (y > 22 && y < 28))) color = accent;
                if (type == 1 && x > 15 && x < 80 && y > 19 && y < 65)
                {
                    color = new Color(.75f, .90f, .86f);
                    if ((x - 65) * (x - 65) + (y - 54) * (y - 54) < 36) color = new Color(1, .80f, .39f);
                    if (y < 20 + Mathf.Max(24 - Mathf.Abs(x - 33), 31 - Mathf.Abs(x - 63))) color = accent;
                }
                if (type == 2 && x > 20 && x < 76 && y > 19 && y < 64)
                {
                    color = accent;
                    if (x > 39 && x < 62 && Mathf.Abs(y - 41) < (62 - x) * .65f) color = Color.white;
                }
                pixels[y * size + x] = color;
            }
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "程序化喂食卡片", filterMode = FilterMode.Bilinear };
            result.SetPixels(pixels); result.Apply(); return result;
        }

        public void Dispose()
        {
            if (root != null) Destroy(root.gameObject);
            Destroy(texture); Destroy(paper); Destroy(sparkle); Destroy(quad); root = null;
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
