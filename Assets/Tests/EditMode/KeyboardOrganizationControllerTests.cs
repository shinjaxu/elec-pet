using System.Reflection;
using System.Threading;
using ElecPet.Core;
using ElecPet.Runtime;
using ElecPet.Windows;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class KeyboardOrganizationControllerTests
    {
        [Test]
        public void OrdinaryPointerNearFamiliarPetDoesNotPauseOrganization()
        {
            var area = new DesktopRect(0, 0, 1000, 700);
            var worker = new DesktopIconWorker(() => new DesktopOrganizationPreview(area), () => 30);
            var organizer = new PetDesktopOrganizer(worker, area);
            var host = new GameObject("键盘空闲整理回归");
            var controller = host.AddComponent<DesktopPetController>();
            var pet = new PrototypePet();
            var type = typeof(DesktopPetController);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Camera camera = null;
            try
            {
                for (int i = 0; i < 100 && !organizer.Active; i++)
                { organizer.Tick(.1, 30, true, new Point2(300, 300)); Thread.Sleep(2); }
                Assert.That(organizer.Active, Is.True);
                var behavior = new PetBehavior(new System.Random(1), .2);
                behavior.Tick(61, false);
                type.GetMethod("CreateCamera", flags).Invoke(controller, null);
                camera = (Camera)type.GetField("petCamera", flags).GetValue(controller);
                var pointer = Input.mousePosition;
                var motion = new PetMotion(new DesktopRect(-10000, -10000, 20000, 20000), 88);
                motion.DragTo(new Point2(pointer.x, pointer.y + 100));
                // 鼠标仅靠近而不命中模型，排除明确抚摸或提起的互动分支。
                pet.Root.position = Vector3.one * 10000;
                type.GetField("motion", flags).SetValue(controller, motion);
                type.GetField("behavior", flags).SetValue(controller, behavior);
                type.GetField("pet", flags).SetValue(controller, pet);
                type.GetField("organizer", flags).SetValue(controller, organizer);
                type.GetField("width", flags).SetValue(controller, Screen.width);
                type.GetField("height", flags).SetValue(controller, Screen.height);
                type.GetField("lastPointer", flags).SetValue(controller, (Vector2)pointer);
                type.GetField("previewIdle", flags).SetValue(controller, 31f);
                type.GetMethod("Update", flags).Invoke(controller, null);
                Assert.That(behavior.State, Is.EqualTo(PetState.Wander), "普通鼠标靠近不能触发问候抢占搬运");
                Assert.That(organizer.Active && !organizer.Suspended, Is.True);
            }
            finally
            {
                organizer.Dispose();
                type.GetField("organizer", flags).SetValue(controller, null);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(pet.Root.gameObject); Object.DestroyImmediate(host);
            }
        }
    }
}
