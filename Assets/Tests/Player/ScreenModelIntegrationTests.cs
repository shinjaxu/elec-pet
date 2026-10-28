using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using ElecPet.Core;
using ElecPet.Runtime;
using ElecPet.Windows;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElecPet.Tests
{
    public sealed class ScreenModelIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest, Explicit("需要本机 Ollama 与 qwen3.5:4b；只发送合成图像。")]
        public IEnumerator LocalModelReturnsACompanionReplyAndSuspensionClearsIt()
        {
            var companion = new PetScreenCompanion(IntPtr.Zero, true);
            try
            {
                Invoke(companion, "StartRequest", "/api/show", OllamaCompanionProtocol.CreateCheck(), true);
                yield return WaitForRequest(companion);
                Invoke(companion, "CompleteRequest", 0d);
                Assert.That(Get<bool>(companion, "ready"), Is.True, "本机模型必须具备视觉能力");

                var clock = Get<ScreenObservationClock>(companion, "clock");
                clock.Observe("合成窗口", 0, true);
                long generation;
                Assert.That(clock.TryBegin(30, out generation), Is.True);
                typeof(PetScreenCompanion).GetField("generation", Private).SetValue(companion, generation);
                byte[] rgba = new byte[640 * 360 * 4];
                for (int y = 0; y < 360; y++)
                for (int x = 0; x < 640; x++)
                {
                    int i = (y * 640 + x) * 4;
                    rgba[i] = (byte)(x < 320 ? 255 : 0);
                    rgba[i + 2] = (byte)(x < 320 ? 0 : 255);
                    rgba[i + 3] = 255;
                }
                Invoke(companion, "SendPicture", new ScreenPixels(640, 360, rgba));
                yield return WaitForRequest(companion);
                clock.Observe("另一个合成窗口", 31, true);
                Invoke(companion, "CompleteRequest", 32d);
                Assert.That(companion.Reply, Is.Null, "已切换窗口的模型结果必须丢弃");
                Assert.That(clock.TryBegin(210, out generation), Is.True);
                typeof(PetScreenCompanion).GetField("generation", Private).SetValue(companion, generation);
                Invoke(companion, "SendPicture", new ScreenPixels(640, 360, rgba));
                yield return WaitForRequest(companion);
                Invoke(companion, "CompleteRequest", 211d);
                Assert.That(companion.Reply, Is.Not.Null.And.Not.Empty, "合成红蓝画面应产生可展示的陪伴回复");
                Assert.That(companion.Reply.Length, Is.LessThanOrEqualTo(80));
                Debug.Log("PASS 本地模型真实视觉回复：" + companion.Reply);
                companion.Suspend(212);
                Assert.That(companion.Reply, Is.Null);
                Assert.That(Get<LocalOllamaRequest>(companion, "request"), Is.Null);
                Assert.That(clock.IsCurrent(generation), Is.False);
                Assert.That(Get<bool>(companion, "ready"), Is.False, "恢复观察前应重新检查模型能力");

                Invoke(companion, "StartRequest", "/api/show", OllamaCompanionProtocol.CreateCheck(), true);
                companion.Tick(213, false);
                var cancelled = Get<LocalOllamaRequest>(companion, "request");
                Assert.That(cancelled == null || cancelled.Cancelled, Is.True, "不可用时须取消在途请求");
                Assert.That(companion.Reply, Is.Null);
                if (cancelled != null)
                {
                    float deadline = Time.realtimeSinceStartup + 6;
                    while (!cancelled.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(cancelled.IsCompleted, Is.True, "取消后后台请求必须结束");
                    companion.Suspend(214);
                }

                // 缺失模型只产生可恢复状态和冷却，不能留下请求或依赖真实截图。
                string missing = "{\"model\":\"elecpet-missing-fixture-" + Guid.NewGuid().ToString("N") + "\"}";
                Invoke(companion, "StartRequest", "/api/show", missing, true);
                yield return WaitForRequest(companion, false);
                Assert.That(Get<LocalOllamaRequest>(companion, "request").Result.StatusCode, Is.EqualTo(404));
                LogAssert.Expect(LogType.Warning, new Regex("屏幕陪伴暂不可用，阶段=模型检查.*HTTP状态=404"));
                Invoke(companion, "CompleteRequest", 215d);
                Assert.That(Get<bool>(companion, "ready"), Is.False);
                Assert.That(Get<double>(companion, "retryAt"), Is.EqualTo(395));
                Assert.That(Get<LocalOllamaRequest>(companion, "request"), Is.Null);
                companion.SetEnabled(false, 216);
                Assert.That(Get<double>(companion, "retryAt"), Is.EqualTo(395), "开关切换不能绕过失败冷却");
            }
            finally { companion.Dispose(); }
        }

        [UnityTest, Explicit("需要本机 Ollama 与 qwen3.5:4b；只发送合成图像。")]
        public IEnumerator ManualObservationReusesModelCheckAndKeepsOneRequest()
        {
            var companion = new PetScreenCompanion(IntPtr.Zero, true);
            try
            {
                Invoke(companion, "StartRequest", "/api/show", OllamaCompanionProtocol.CreateCheck(), true);
                var check = Get<LocalOllamaRequest>(companion, "request");
                var target = new ObservedWindow(new IntPtr(1), "合成手动窗口", new DesktopRect(0, 0, 640, 360));
                Assert.That(Invoke(companion, "TryQueueImmediate", target, 1L, 1d), Is.True,
                    "能力检查期间可预留一次手动观察");
                long generation = Get<long>(companion, "generation");
                var clock = Get<ScreenObservationClock>(companion, "clock");
                Assert.That(clock.IsCurrent(generation), Is.True);
                Assert.That(Get<bool>(companion, "manualPending"), Is.True);
                Assert.That(Invoke(companion, "TryQueueImmediate", target, 1L, 3d), Is.False,
                    "超过防抖间隔后，待处理手动观察仍不能叠加");
                Assert.That(Get<LocalOllamaRequest>(companion, "request"), Is.SameAs(check),
                    "手动点击必须复用已有能力检查");
                yield return WaitForRequest(companion);
                Invoke(companion, "CompleteRequest", 4d);
                Assert.That(Get<bool>(companion, "ready"), Is.True);
                Assert.That(Get<bool>(companion, "manualPending"), Is.True,
                    "能力检查完成后须保留手动观察，等待领取捕获");
                Assert.That(Get<long>(companion, "generation"), Is.EqualTo(generation));

                // 用合成像素模拟领取捕获，不调用 Tick 或读取真实前台画面。
                typeof(PetScreenCompanion).GetField("manualPending", Private).SetValue(companion, false);
                byte[] rgba = new byte[640 * 360 * 4];
                for (int i = 0; i < rgba.Length; i += 4)
                {
                    bool red = i / 4 % 640 < 320;
                    rgba[i] = (byte)(red ? 255 : 0);
                    rgba[i + 2] = (byte)(red ? 0 : 255);
                    rgba[i + 3] = 255;
                }
                Invoke(companion, "SendPicture", new ScreenPixels(640, 360, rgba));
                var chat = Get<LocalOllamaRequest>(companion, "request");
                Assert.That(Invoke(companion, "TryQueueImmediate", target, 1L, 5d), Is.False,
                    "聊天期间不能再预留手动观察");
                Assert.That(Get<LocalOllamaRequest>(companion, "request"), Is.SameAs(chat));
                Assert.That(Get<long>(companion, "generation"), Is.EqualTo(generation));
                yield return WaitForRequest(companion);
                Invoke(companion, "CompleteRequest", 6d);
                Assert.That(companion.Reply, Is.Not.Null.And.Not.Empty);
                Assert.That(companion.Reply.Length, Is.LessThanOrEqualTo(80));

                Assert.That(Invoke(companion, "TryQueueImmediate", target, 1L, 7d), Is.True);
                Assert.That(Get<bool>(companion, "manualPending"), Is.True);
                companion.Suspend(8);
                Assert.That(Get<bool>(companion, "manualPending"), Is.False,
                    "暂停必须清除尚未领取的手动观察");
            }
            finally { companion.Dispose(); }
        }

        private static IEnumerator WaitForRequest(PetScreenCompanion companion, bool success = true)
        {
            var request = Get<LocalOllamaRequest>(companion, "request");
            float deadline = Time.realtimeSinceStartup + 65;
            while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.IsCompleted, Is.True, "本地请求须在超时内结束");
            Assert.That(request.Result.Success, Is.EqualTo(success), "本地模型请求状态=" + request.Result.StatusCode);
        }

        private static T Get<T>(object target, string name)
        { return (T)target.GetType().GetField(name, Private).GetValue(target); }
        private static object Invoke(object target, string name, params object[] args)
        { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    }
}
