using System;
using System.Reflection;
using System.Runtime.Serialization;
using ElecPet.Core;
using ElecPet.Windows;
using NUnit.Framework;

namespace ElecPet.Tests
{
    public sealed class NativeMouseMessageTests
    {
        [Test]
        public void PressAndReleaseBetweenFramesAreBothAvailable()
        {
            var window = CreateWindowShell();
            Send(window, 0x201, 12, 34);
            Send(window, 0x202, 15, 38);
            AssertPoint(Take(window, "TakeLeftPress"), 12, 34);
            AssertPoint(Take(window, "TakeLeftRelease"), 15, 38);
        }

        [Test]
        public void MouseEventsAreConsumedOnlyOnce()
        {
            var window = CreateWindowShell();
            Send(window, 0x201, 12, 34);
            Send(window, 0x202, 15, 38);
            Assert.That(Take(window, "TakeLeftPress").HasValue, Is.True);
            Assert.That(Take(window, "TakeLeftRelease").HasValue, Is.True);
            Assert.That(Take(window, "TakeLeftPress").HasValue, Is.False);
            Assert.That(Take(window, "TakeLeftRelease").HasValue, Is.False);
        }

        [Test]
        public void NewPressDiscardsThePreviousRelease()
        {
            var window = CreateWindowShell();
            Send(window, 0x201, 12, 34);
            Send(window, 0x202, 15, 38);
            Send(window, 0x201, 24, 48);
            AssertPoint(Take(window, "TakeLeftPress"), 24, 48);
            Assert.That(Take(window, "TakeLeftRelease").HasValue, Is.False);
        }

        [Test]
        public void MouseMessagesIncludeNegativeVirtualDesktopOrigin()
        {
            var window = CreateWindowShell();
            typeof(NativeWindow).GetField("<Bounds>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(window, new DesktopRect(-1920, -1080, 3840, 2160));
            Send(window, 0x201, 12, 34);
            Send(window, 0x202, 15, 38);
            AssertPoint(Take(window, "TakeLeftPress"), -1908, -1046);
            AssertPoint(Take(window, "TakeLeftRelease"), -1905, -1042);
        }

        [Test]
        public void MouseMessagesKeepSignedClientCoordinates()
        {
            var window = CreateWindowShell();
            Send(window, 0x201, -32768, -1);
            Send(window, 0x202, 32767, -32768);
            AssertPoint(Take(window, "TakeLeftPress"), -32768, -1);
            AssertPoint(Take(window, "TakeLeftRelease"), 32767, -32768);
        }

        private static NativeWindow CreateWindowShell()
        {
            // 先验证入口，旧实现须在发送消息前失败，避免调用原生窗口过程。
            foreach (string name in new[] { "TakeLeftPress", "TakeLeftRelease" })
            {
                var method = typeof(NativeWindow).GetMethod(name, Type.EmptyTypes);
                Assert.That(method, Is.Not.Null, "NativeWindow 缺少 " + name);
                Assert.That(method.ReturnType, Is.EqualTo(typeof(Point2?)));
            }
            // 不创建或释放真实窗口；这个壳仅用于两种左键消息的缓存检查。
            return (NativeWindow)FormatterServices.GetUninitializedObject(typeof(NativeWindow));
        }

        private static void Send(NativeWindow window, uint message, int x, int y)
        {
            int coordinates = unchecked((ushort)x | ((ushort)y << 16));
            var callback = typeof(NativeWindow).GetMethod("HandleMessage", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (IntPtr)callback.Invoke(window, new object[] { IntPtr.Zero, message, IntPtr.Zero, new IntPtr(coordinates) });
            Assert.That(result, Is.EqualTo(IntPtr.Zero));
        }

        private static Point2? Take(NativeWindow window, string name)
        { return (Point2?)typeof(NativeWindow).GetMethod(name).Invoke(window, null); }

        private static void AssertPoint(Point2? point, int x, int y)
        {
            Assert.That(point.HasValue, Is.True);
            Assert.That(point.Value.X, Is.EqualTo(x));
            Assert.That(point.Value.Y, Is.EqualTo(y));
        }
    }
}
