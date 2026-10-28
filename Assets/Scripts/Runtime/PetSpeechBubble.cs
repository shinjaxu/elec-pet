using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using TextAsset = UnityEngine.TextCore.Text.TextAsset;

namespace ElecPet.Runtime
{
    // 陪伴文字使用独立外观，避免改变需要即时反馈的喂食提示。
    public sealed class PetSpeechBubble : IDisposable
    {
        private readonly Color cream = new Color32(255, 253, 246, 255);
        private readonly Color border = new Color32(218, 226, 214, 255);
        private Texture2D body, shadow, tail;
        private GUIStyle bodyStyle, shadowStyle, textStyle, quoteStyle;
        private Font font;
        private FontAsset emojiFont;
        private TextSettings emojiSettings;
        private bool emojiPrepared;
        private string lastMessage;
        private double appearedAt, lastSeenAt;

        public Rect Draw(string message, Vector2 anchor, Vector2 viewport, double now, float scale = 1,
            double expiresAt = double.PositiveInfinity)
        {
            Prepare();
            scale = Mathf.Max(1, scale);
            textStyle.fontSize = Mathf.RoundToInt(17 * scale);
            quoteStyle.fontSize = Mathf.RoundToInt(32 * scale);
            var content = new GUIContent(message);
            float margin = 16 * scale, insetLeft = 42 * scale, insetRight = 22 * scale, padding = 18 * scale;
            float maxWidth = Mathf.Min(360 * scale, viewport.x - margin * 2);
            // IMGUI 绘制时对齐设备像素，宽度向上取整，避免紧贴边界的 Emoji 整组多换一行。
            float width = Mathf.Ceil(Mathf.Clamp(textStyle.CalcSize(content).x + insetLeft + insetRight,
                Mathf.Min(188 * scale, maxWidth), maxWidth));
            float textWidth = width - insetLeft - insetRight;
            float height = textStyle.CalcHeight(content, textWidth) + padding * 2;

            // 顶部空间不足时翻到宠物下方，尾巴仍指向宠物；四边留出阴影所需的余量。
            float gap = 46 * scale;
            bool above = anchor.y - gap - height >= margin;
            if (!above && anchor.y + gap + height > viewport.y - margin)
                above = anchor.y > viewport.y / 2;
            float x = Mathf.Clamp(anchor.x - width / 2, margin, Mathf.Max(margin, viewport.x - width - margin));
            float y = above ? anchor.y - gap - height : anchor.y + gap;
            y = Mathf.Clamp(y, margin, Mathf.Max(margin, viewport.y - height - margin));
            var rect = new Rect(x, y, width, height);

            if (message != lastMessage || now - lastSeenAt > .3)
            {
                lastMessage = message;
                appearedAt = now;
            }
            lastSeenAt = now;
            float appear = Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(now - appearedAt) / .22f));
            float disappear = Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(expiresAt - now) / .35f));
            rect.y += (above ? 1 : -1) * (1 - appear) * 6 * scale;
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * appear * disappear);
                GUI.Box(new Rect(rect.x - 12 * scale, rect.y - 10 * scale, rect.width + 24 * scale, rect.height + 26 * scale),
                    GUIContent.none, shadowStyle);
                GUI.Box(rect, GUIContent.none, bodyStyle);
                float tailX = Mathf.Clamp(anchor.x, rect.x + 30 * scale, rect.xMax - 30 * scale);
                var tailRect = new Rect(tailX - 16 * scale, above ? rect.yMax - 2 * scale : rect.y - 16 * scale,
                    32 * scale, 18 * scale);
                GUI.DrawTextureWithTexCoords(tailRect, tail, above ? new Rect(0, 0, 1, 1) : new Rect(0, 1, 1, -1));
                GUI.Label(new Rect(rect.x + 16 * scale, rect.y + 8 * scale, 25 * scale, 32 * scale), "“", quoteStyle);
                GUI.Label(new Rect(rect.x + insetLeft, rect.y + padding, textWidth, height - padding * 2), content, textStyle);
            }
            finally { GUI.color = previous; }
            return rect;
        }

        private void Prepare()
        {
            if (body != null) return;
            PrepareEmojiFallback();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 17);
            body = RoundedTexture(false);
            shadow = RoundedTexture(true);
            tail = TailTexture();
            bodyStyle = new GUIStyle { border = new RectOffset(24, 24, 24, 24) };
            bodyStyle.normal.background = body;
            shadowStyle = new GUIStyle { border = new RectOffset(36, 36, 36, 36) };
            shadowStyle.normal.background = shadow;
            textStyle = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, richText = false,
                alignment = TextAnchor.UpperLeft, padding = new RectOffset(), margin = new RectOffset() };
            textStyle.normal.textColor = new Color32(49, 62, 55, 255);
            quoteStyle = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold, wordWrap = false };
            quoteStyle.normal.textColor = new Color32(121, 154, 125, 255);
        }

        private void PrepareEmojiFallback()
        {
            if (emojiPrepared || Application.platform != RuntimePlatform.WindowsPlayer) return;
            emojiPrepared = true;
            // Unity 6 的编辑器自带 Emoji 回退，Player 未配置；IMGUI 尚未公开此运行时设置入口。
            var runtimeType = typeof(GUIStyle).Assembly.GetType("UnityEngine.RuntimeTextSettings");
            var property = runtimeType.GetProperty("defaultTextSettings", BindingFlags.Static | BindingFlags.NonPublic);
            emojiSettings = (TextSettings)property.GetValue(null);
            // 显式加载项目中的彩色文字着色器，避免 Player 剥离仅在编辑器中存在的渲染资源。
            Resources.Load<Shader>("EmojiSprite");
            FontEngine.InitializeFontEngine();
            FontEngine.GetSystemFontNames();
            emojiFont = FontAsset.CreateFontAsset("Segoe UI Emoji", "Regular", 90);
            // 字体缺失时仍显示原有中文，只尝试初始化一次，避免每帧重复分配和报错。
            if (emojiFont == null) return;
            emojiFont.hideFlags = HideFlags.HideAndDontSave;
            // 当前标准 TextCore 默认只开字距调整，启用系统字体提供的肤色与连接序列合字。
            var settingsType = typeof(TextSettings).Assembly.GetType("UnityEngine.TextCore.Text.TextGenerationSettings");
            var features = (List<OTL_FeatureTag>)settingsType
                .GetField("fontFeatures", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (!features.Contains(OTL_FeatureTag.liga)) features.Add(OTL_FeatureTag.liga);
            var fallbacks = emojiSettings.emojiFallbackTextAssets == null
                ? new List<TextAsset>() : new List<TextAsset>(emojiSettings.emojiFallbackTextAssets);
            fallbacks.Add(emojiFont);
            // 通过属性回写，通知 TextCore 刷新原生字体设置，保留中文字体和已有回退资源。
            emojiSettings.emojiFallbackTextAssets = fallbacks;
        }

        private Texture2D RoundedTexture(bool isShadow)
        {
            const int size = 96;
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float half = isShadow ? 34 : 48, radius = isShadow ? 18 : 20;
                float dx = Mathf.Abs(x + .5f - 48) - (half - radius);
                float dy = Mathf.Abs(y + .5f - 48) - (half - radius);
                float distance = new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;
                Color color;
                if (isShadow)
                    color = new Color(.12f, .17f, .13f, .10f * Mathf.Exp(-Mathf.Pow(Mathf.Max(distance, 0) / 7, 2) / 2));
                else
                {
                    color = Color.Lerp(border, cream, Mathf.Clamp01(-distance - .1f));
                    color.a = Mathf.Clamp01(.5f - distance);
                }
                colors[y * size + x] = color;
            }
            return Texture(size, size, colors);
        }

        private Texture2D TailTexture()
        {
            const int width = 64, height = 36;
            var colors = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float halfWidth = (y + .5f) * 28 / height;
                float distance = (halfWidth - Mathf.Abs(x + .5f - width / 2f)) / 1.27f;
                Color color = Color.Lerp(border, cream, Mathf.Clamp01((distance - 1) / 2));
                // 根部不画横线，覆盖正文边框后连接成一个完整的气泡。
                color.a = Mathf.Clamp01(distance + .5f);
                colors[y * width + x] = color;
            }
            return Texture(width, height, colors);
        }

        private static Texture2D Texture(int width, int height, Color[] colors)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(colors);
            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            if (emojiFont != null)
            {
                if (emojiSettings != null && emojiSettings.emojiFallbackTextAssets != null)
                {
                    var fallbacks = new List<TextAsset>(emojiSettings.emojiFallbackTextAssets);
                    fallbacks.Remove(emojiFont);
                    emojiSettings.emojiFallbackTextAssets = fallbacks;
                }
                Release(emojiFont.material);
                foreach (var atlas in emojiFont.atlasTextures) Release(atlas);
                Release(emojiFont);
                emojiFont = null;
                emojiSettings = null;
            }
            Release(body); Release(shadow); Release(tail); Release(font);
            body = shadow = tail = null;
            font = null;
        }

        private static void Release(UnityEngine.Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }
    }
}
