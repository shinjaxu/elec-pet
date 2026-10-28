using System.Reflection;
using ElecPet.Core;
using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class ScreenClickControllerTests
    {
        [Test]
        public void MovementOnTheReleaseFrameIsHandledAsDragging()
        {
            var host = new GameObject("松开帧位移回归");
            var controller = host.AddComponent<DesktopPetController>();
            var type = typeof(DesktopPetController);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var behavior = new PetBehavior(new System.Random(1));
            var motion = new PetMotion(new DesktopRect(0, 0, 1000, 800), 88);
            try
            {
                type.GetField("behavior", flags).SetValue(controller, behavior);
                type.GetField("motion", flags).SetValue(controller, motion);
                var start = new Vector2((float)motion.Position.X, (float)motion.Position.Y);
                type.GetMethod("BeginPress", flags).Invoke(controller, new object[] { start, host.transform });
                // 两次采样间移动并松手，期间没有按住帧可调用 DragPress。
                type.GetMethod("EndPress", flags).Invoke(controller, new object[] { start + Vector2.right * 20 });
                Assert.That(behavior.State, Is.EqualTo(PetState.Recovering), "拖动松手应放下，不能执行轻点触碰或看图");
                Assert.That(motion.Position.X, Is.EqualTo(start.x + 20));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
