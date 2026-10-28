using System;
using System.IO;
using ElecPet.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ElecPet.Editor
{
    public sealed class SpeechBubblePreview : EditorWindow
    {
        private const int Width = 800, Height = 520;
        private static readonly Vector2 Viewport = new Vector2(Width, Height);
        private static readonly PreviewCase[] Cases = {
            new PreviewCase("short", "这页有点意思，我陪你看。", new Vector2(400, 360)),
            new PreviewCase("long", "这一页的信息有点多，不过你已经找到重点了。我们把眼前这一小步慢慢看清楚，再一起继续往下走，我就在旁边陪着你。等这段忙完，我们再一起看看下一页会藏着什么新发现呀。", new Vector2(400, 370), dark: true),
            new PreviewCase("top-left", "在这里也能听清我的悄悄话。", new Vector2(76, 68), below: true),
            new PreviewCase("top-right", "在这里也能听清我的悄悄话。", new Vector2(724, 68), dark: true, below: true),
            new PreviewCase("bottom-left", "我在角落陪你，把这一小步做好。", new Vector2(76, 452), dark: true),
            new PreviewCase("bottom-right", "我在角落陪你，把这一小步做好。", new Vector2(724, 452)),
            new PreviewCase("dpi125", "这个 API 的第 3 步，我们慢慢看。", new Vector2(400, 390), 1.25f),
            new PreviewCase("dpi150", "这个 API 的第 3 步，我们慢慢看。", new Vector2(400, 400), 1.5f, true),
            new PreviewCase("multiline", "陪你一起慢慢来。\n这一页的信息有点多，\n我们先看清眼前这一小步，\n再一起往下走。", new Vector2(400, 390)),
            new PreviewCase("emoji", "这页有点热闹 😄🐉✨❤️\n我陪你慢慢看 👩‍💻👍🏽🇨🇳1️⃣", new Vector2(400, 390)),
            new PreviewCase("emoji-dpi150", "我在这儿陪你 😄🐉✨❤️\n一起继续吧 👩‍💻👍🏽🇨🇳1️⃣", new Vector2(400, 410), 1.5f, true)
        };
        private PetSpeechBubble bubble;
        private Texture2D circle;
        private string directory, diagnostics;
        private int caseIndex, repaintFrames;
        private double deadline;
        private bool captureQueued, finished;
        private Rect bounds;

        [MenuItem("桌宠/导出陪伴气泡预览")]
        public static void Render()
        {
            var window = CreateInstance<SpeechBubblePreview>();
            window.directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "docs/previews/speech-bubble");
            window.diagnostics = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/SpeechBubbleValidation/preview.log");
            Directory.CreateDirectory(window.directory);
            Directory.CreateDirectory(Path.GetDirectoryName(window.diagnostics));
            File.WriteAllText(window.diagnostics, "开始导出合成气泡预览。\n");
            window.titleContent = new GUIContent("陪伴气泡合成预览");
            window.minSize = window.maxSize = Viewport;
            window.position = new Rect(60, 60, Width, Height);
            window.bubble = new PetSpeechBubble();
            window.ShowUtility();
            window.Focus();
            window.deadline = EditorApplication.timeSinceStartup + 10;
            EditorApplication.update += window.AwaitRepaint;
            window.Repaint();
        }

        private void OnGUI()
        {
            if (bubble == null || finished) return;
            var previousSkin = GUI.skin;
            try
            {
                GUI.skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Game);
                var item = Cases[caseIndex];
                DrawBackground(item.Dark);
                DrawAnchor(item.Anchor);
                // 连续的小步时间让实际淡入完成，避免静态预览被误判成新一轮发言。
                bounds = bubble.Draw(item.Message, item.Anchor, Viewport, repaintFrames * .1, item.Scale);
                if (Event.current.type != EventType.Repaint) return;
                repaintFrames++;
                if (repaintFrames >= 6 && !captureQueued)
                {
                    captureQueued = true;
                    EditorApplication.delayCall += CaptureCurrent;
                }
            }
            catch (Exception exception) { Finish(exception); }
            finally { GUI.skin = previousSkin; }
        }

        private void AwaitRepaint()
        {
            if (finished) return;
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(new InvalidOperationException("合成预览窗口在十秒内未完成绘制；当前 GUIView 不可用。"));
                return;
            }
            if (!hasFocus) Focus();
            Repaint();
        }

        private void CaptureCurrent()
        {
            if (finished) return;
            RenderTexture target = null;
            Texture2D pixels = null;
            var previous = RenderTexture.active;
            try
            {
                var item = Cases[caseIndex];
                // 检查实际绘制返回的边界，避免只导出好看的中心示例而遗漏贴边问题。
                if (bounds.width <= 0 || bounds.height <= 0 || bounds.xMin < 7.5f || bounds.yMin < 7.5f
                    || bounds.xMax > Width - 7.5f || bounds.yMax > Height - 7.5f)
                    throw new InvalidOperationException(item.Name + "：气泡未保留屏幕安全边距，边界=" + bounds);
                if (item.Below && bounds.yMin <= item.Anchor.y)
                    throw new InvalidOperationException(item.Name + "：顶部空间不足时没有移到锚点下方。");
                target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
                target.Create();
                // 仅读取此专用编辑器窗口的 GPU 缓冲，不截取用户桌面或其他软件。
                bool captured = InternalEditorUtility.CaptureEditorWindow(this, target);
                if (!captured) throw new InvalidOperationException("合成预览窗口捕获失败；当前 GUIView 不可用或没有焦点。");
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, item.Name + ".png"), pixels.EncodeToPNG());
                File.AppendAllText(diagnostics, item.Name + "：边界=" + bounds + "，缩放=" + item.Scale + "，通过。\n");
                caseIndex++;
                if (caseIndex == Cases.Length) { Finish(null); return; }
                bubble.Dispose();
                bubble = new PetSpeechBubble();
                repaintFrames = 0;
                captureQueued = false;
                deadline = EditorApplication.timeSinceStartup + 10;
                Repaint();
            }
            catch (Exception exception) { Finish(exception); }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) DestroyImmediate(pixels);
                if (target != null) { target.Release(); DestroyImmediate(target); }
            }
        }

        private void DrawBackground(bool dark)
        {
            EditorGUI.DrawRect(new Rect(0, 0, Width, Height), dark ? new Color(.105f, .13f, .17f) : new Color(.92f, .935f, .95f));
            Color panel = dark ? new Color(.15f, .18f, .23f) : new Color(.98f, .985f, .99f);
            EditorGUI.DrawRect(new Rect(26, 24, 148, 472), panel);
            EditorGUI.DrawRect(new Rect(196, 24, 578, 54), panel);
            EditorGUI.DrawRect(new Rect(196, 100, 578, 396), panel);
            Color line = dark ? new Color(.24f, .28f, .34f) : new Color(.80f, .83f, .86f);
            for (int i = 0; i < 8; i++) EditorGUI.DrawRect(new Rect(222, 130 + i * 35, i % 3 == 0 ? 330 : 472, 5), line);
        }

        private void DrawAnchor(Vector2 anchor)
        {
            if (circle == null)
            {
                circle = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var colors = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    colors[y * 32 + x] = new Color(1, 1, 1, Mathf.Clamp01(16 - Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f))));
                circle.SetPixels(colors); circle.Apply();
            }
            var previous = GUI.color;
            GUI.color = new Color(.08f, .11f, .14f);
            GUI.DrawTexture(new Rect(anchor.x - 16, anchor.y - 16, 32, 32), circle);
            GUI.color = new Color(.63f, .82f, .38f);
            GUI.DrawTexture(new Rect(anchor.x - 9, anchor.y - 3, 5, 7), circle);
            GUI.DrawTexture(new Rect(anchor.x + 4, anchor.y - 3, 5, 7), circle);
            GUI.color = previous;
        }

        private void Finish(Exception exception)
        {
            if (finished) return;
            finished = true;
            EditorApplication.update -= AwaitRepaint;
            EditorApplication.delayCall -= CaptureCurrent;
            if (exception == null)
            {
                File.AppendAllText(diagnostics, Cases.Length + " 个合成气泡预览全部完成。\n");
                Debug.Log("陪伴气泡预览已导出至 docs/previews/speech-bubble，共 " + Cases.Length + " 张合成图。");
            }
            else
            {
                File.AppendAllText(diagnostics, exception + "\n");
                Debug.LogException(exception);
            }
            Close();
            if (Application.isBatchMode) EditorApplication.Exit(exception == null ? 0 : 1);
        }

        private void OnDestroy()
        {
            EditorApplication.update -= AwaitRepaint;
            EditorApplication.delayCall -= CaptureCurrent;
            if (bubble != null) { bubble.Dispose(); bubble = null; }
            if (circle != null) DestroyImmediate(circle);
        }

        private sealed class PreviewCase
        {
            public readonly string Name, Message;
            public readonly Vector2 Anchor;
            public readonly float Scale;
            public readonly bool Dark, Below;
            public PreviewCase(string name, string message, Vector2 anchor, float scale = 1, bool dark = false, bool below = false)
            { Name = name; Message = message; Anchor = anchor; Scale = scale; Dark = dark; Below = below; }
        }
    }
}
