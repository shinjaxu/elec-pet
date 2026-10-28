using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ElecPet.Core;
using ElecPet.Runtime;
using ElecPet.Windows;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElecPet.Tests
{
    public sealed class ManualScreenCompanionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private PetScreenCompanion companion;

        [SetUp] public void SetUp() { companion = new PetScreenCompanion(IntPtr.Zero, true); }
        [TearDown] public void TearDown() { companion.Dispose(); }

        [Test]
        public void ImmediateQueueReservesTheWindowAndClearsThePreviousReply()
        {
            typeof(PetScreenCompanion).GetProperty("Reply").SetValue(companion, "上一句");
            Assert.That(Queue("页面A", 0), Is.True);
            Assert.That(Get<bool>("manualPending"), Is.True);
            Assert.That(Get<ScreenObservationClock>("clock").IsCurrent(Get<long>("generation")), Is.True);
            Assert.That(companion.Reply, Is.Null);
            Assert.That(Get<Task<ScreenPixels>>("capture"), Is.Null, "仍需由 Tick 核对可用状态后捕获");
        }

        [Test]
        public void RepeatedClickDoesNotReplaceThePendingObservation()
        {
            Assert.That(Queue("页面A", 0), Is.True);
            long generation = Get<long>("generation");
            Assert.That(Queue("页面B", 3), Is.False);
            Assert.That(Get<long>("generation"), Is.EqualTo(generation));
            Assert.That(Get<ObservedWindow>("observed").Identity, Is.EqualTo("页面A"));
        }

        [Test]
        public void CaptureInFlightRejectsAnotherClick()
        {
            var pending = new TaskCompletionSource<ScreenPixels>();
            Set("capture", pending.Task);
            try { Assert.That(Queue("页面A", 0), Is.False); }
            finally { pending.SetResult(null); }
        }

        [Test]
        public void ManualRetryBypassesTheModelFailureDelay()
        {
            Set("retryAt", 180d);
            Assert.That(Queue("页面A", 2), Is.True);
            Assert.That(Get<double>("retryAt"), Is.EqualTo(2));
        }

        [Test]
        public void SuspensionDropsTheQueuedObservation()
        {
            Assert.That(Queue("页面A", 0), Is.True);
            long generation = Get<long>("generation");
            companion.Suspend(1);
            Assert.That(Get<bool>("manualPending"), Is.False);
            Assert.That(Get<ScreenObservationClock>("clock").IsCurrent(generation), Is.False);
        }

        [TestCase("页面B", 0L)]
        [TestCase("页面A", 1L)]
        public void WindowOrForegroundRevisionChangeDropsTheQueuedObservation(string identity, long revision)
        {
            Assert.That(Queue("页面A", 0), Is.True);
            Invoke("UpdateObservedWindow", Window(identity), revision, 1d);
            Assert.That(Get<bool>("manualPending"), Is.False, "切走再切回也不能继续旧点击");
        }

        [Test]
        public void ModelFailureDropsTheQueuedObservation()
        {
            Assert.That(Queue("页面A", 0), Is.True);
            LogAssert.Expect(LogType.Warning, new Regex("屏幕陪伴暂不可用，阶段=模型检查"));
            Invoke("Fail", 1d, "模型检查", 404L, null);
            Assert.That(Get<bool>("manualPending"), Is.False);
        }

        private bool Queue(string identity, double now)
        { return (bool)Invoke("TryQueueImmediate", Window(identity), 0L, now); }

        private static ObservedWindow Window(string identity)
        { return new ObservedWindow(new IntPtr(1), identity, new DesktopRect(0, 0, 100, 100)); }

        private T Get<T>(string name)
        { return (T)typeof(PetScreenCompanion).GetField(name, Private).GetValue(companion); }

        private void Set(string name, object value)
        { typeof(PetScreenCompanion).GetField(name, Private).SetValue(companion, value); }

        private object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(PetScreenCompanion).GetMethod(name, Private);
            Assert.That(method, Is.Not.Null, "缺少手动观察编排：" + name);
            return method.Invoke(companion, arguments);
        }
    }
}
