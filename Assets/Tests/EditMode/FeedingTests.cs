using NUnit.Framework;
using System;
using System.Reflection;
using ElecPet.Core;
using ElecPet.Runtime;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class FeedingTests
    {
        [Test]
        public void FilesAreConsumedOnlyAfterConfirmedRecycling()
        { Assert.That(FeedingChecks.Run(), Is.GreaterThan(20)); }

        [Test]
        public void PickingUpWhileRecyclingKeepsTheCarriedPose()
        {
            var host = new GameObject("回收等待时提起宠物回归");
            var controller = host.AddComponent<DesktopPetController>();
            var pet = new PrototypePet();
            var type = typeof(DesktopPetController);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Camera camera = null;
            try
            {
                type.GetMethod("CreateCamera", flags).Invoke(controller, null);
                camera = (Camera)type.GetField("petCamera", flags).GetValue(controller);
                var behavior = new PetBehavior(new System.Random(1));
                behavior.PickUp();
                var motion = new PetMotion(new DesktopRect(0, 0, 1000, 800), 88);
                type.GetField("behavior", flags).SetValue(controller, behavior);
                type.GetField("motion", flags).SetValue(controller, motion);
                type.GetField("pet", flags).SetValue(controller, pet);
                type.GetField("pressing", flags).SetValue(controller, true);
                type.GetField("dragging", flags).SetValue(controller, true);
                var flow = ((PetFileFeeding)type.GetField("feeding", flags).GetValue(controller)).Flow;
                // 只创建等待结果的内存会话，不提交任何真实文件操作。
                flow.Begin(new[] { "pending.png" }); flow.Tick(4); flow.TakeRecyclePath(); flow.Cancel();
                for (int i = 0; i < 30; i++)
                    type.GetMethod("UpdatePresentation", flags).Invoke(controller,
                        new object[] { Vector2.zero, motion.Position, 1f / 30 });
                Assert.That(flow.Active, Is.True, "已提交状态必须继续等待结果");
                foreach (var part in pet.Root.GetComponentsInChildren<Transform>())
                    if (part.name == "前爪" || part.name == "后爪")
                        Assert.That(part.localPosition.y, Is.GreaterThan(-.49f), "回收等待不能覆盖提起的悬爪姿态");
            }
            finally
            {
                if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
                UnityEngine.Object.DestroyImmediate(pet.Root.gameObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
