using System.Collections;
using System.IO;
using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElecPet.Tests
{
    public sealed class SpeechBubbleRenderingTests
    {
        [UnityTest, Explicit("仅导出测试窗口自身的合成气泡，不读取桌面或调用模型。")]
        public IEnumerator ExportEmojiBubbleFromPlayerFramebuffer()
        {
            const string message = "这页有点热闹 😄🐉✨❤️\n我陪你慢慢看 👩‍💻👍🏽🇨🇳1️⃣";
            string rawJson = "{\"done\":true,\"message\":{\"content\":\"这页有点热闹 😄🐉✨❤️\\n我陪你慢慢看 👩‍💻👍🏽🇨🇳1️⃣\"}}";
            Assert.That(OllamaCompanionProtocol.ReadReply(rawJson), Is.EqualTo(message));
            Assert.That(OllamaCompanionProtocol.ReadReply("{\"done\":true,\"message\":{\"content\":\"\\uD83D\\uDE04\"}}"),
                Is.EqualTo("😄"), "转义代理对也必须完整交给字体渲染");
            Screen.SetResolution(800, 520, FullScreenMode.Windowed);
            var host = new GameObject("表情气泡合成渲染");
            var preview = host.AddComponent<EmojiBubbleFixture>();
            Texture2D pixels = null;
            try
            {
                foreach (string sample in new[] { message, "😄🐉✨❤️👍🏽",
                    "这个页面的信息有点多 😄，我们先找重点，再慢慢看。🐉✨ 你负责决定方向，我负责在旁边陪着，顺便吐槽两句。😅" })
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    preview.Message = sample;
                    preview.Scale = scale;
                    for (int i = 0; i < 20; i++) yield return null;
                    yield return new WaitForEndOfFrame();
                    Assert.That(preview.Bounds.width, Is.GreaterThan(0));
                    pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    // ReadPixels 仅读取本测试 Player 的游戏缓冲，不涉及用户桌面合成图。
                    pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    pixels.Apply();
                    string directory = Path.GetDirectoryName(Application.dataPath);
                    string suffix = sample == message ? "" : sample.StartsWith("😄") ? "-only" : "-long";
                    string name = "emoji-player" + suffix + (scale == 1 ? "" : "-dpi150") + ".png";
                    File.WriteAllBytes(Path.Combine(directory, name), pixels.EncodeToPNG());
                    Debug.Log("表情气泡合成图已保存：" + Path.Combine(directory, name));
                    int emojiPixels = 0;
                    foreach (Color32 color in pixels.GetPixels32())
                        if (color.r > 200 && color.g > 100 && color.g < 245 && color.b < 120) emojiPixels++;
                    Assert.That(emojiPixels, Is.GreaterThan(10), "笑脸应呈现彩色字形，不能退回缺字方框");
                    AssertBottomPadding(pixels, preview.Bounds, scale);
                    Object.Destroy(pixels);
                    pixels = null;
                }
            }
            finally
            {
                if (pixels != null) Object.Destroy(pixels);
                Object.Destroy(host);
            }
        }

        private static void AssertBottomPadding(Texture2D pixels, Rect bounds, float scale)
        {
            for (int y = Mathf.CeilToInt(bounds.yMax - 12 * scale); y < bounds.yMax - 3 * scale; y++)
            for (int x = Mathf.CeilToInt(bounds.x + 42 * scale); x < bounds.xMax - 22 * scale; x++)
            {
                Color32 color = pixels.GetPixel(x, pixels.height - 1 - y);
                bool darkText = color.r < 100 && color.g < 110 && color.b < 110;
                bool colorGlyph = Mathf.Max(color.r, color.g, color.b) - Mathf.Min(color.r, color.g, color.b) > 60
                    && Mathf.Min(color.r, color.g, color.b) < 160;
                Assert.That(darkText || colorGlyph, Is.False,
                    "字体回退换行后，正文不能侵入气泡底部留白");
            }
        }
    }

    public sealed class EmojiBubbleFixture : MonoBehaviour
    {
        public string Message;
        public float Scale = 1;
        public Rect Bounds;
        private readonly PetSpeechBubble bubble = new PetSpeechBubble();
        private Texture2D background;

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(Message)) return;
            if (background == null)
            {
                background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(.92f, .935f, .95f));
                background.Apply();
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background);
            Bounds = bubble.Draw(Message, new Vector2(Screen.width / 2f, Screen.height * .72f),
                new Vector2(Screen.width, Screen.height), Time.realtimeSinceStartupAsDouble, Scale);
        }

        private void OnDestroy()
        {
            bubble.Dispose();
            if (background != null) Destroy(background);
        }
    }
}
