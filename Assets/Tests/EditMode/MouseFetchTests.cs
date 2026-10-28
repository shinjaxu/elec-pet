using System.Reflection;
using ElecPet.Core;
using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class MouseFetchTests
    {
        [Test]
        public void GestureAndFetchLifecycle() { Assert.That(MouseFetchChecks.Run(), Is.EqualTo(16)); }

        [Test]
        public void StalkingLowersBodyAndTracksHead()
        {
            var pet = new PrototypePet();
            try
            {
                float time = 0;
                for (int i = 0; i < 40; i++) pet.Animate(Vector2.left, time += 1f / 30, false, 0, stalking: 1);
                Assert.That(pet.Root.Find("躯干").localPosition.y, Is.LessThan(-.22f));
                var head = pet.Root.Find("躯干/注视点");
                Assert.That(head, Is.Not.Null);
                Quaternion before = head.localRotation;
                for (int i = 0; i < 40; i++) pet.Animate(Vector2.right, time += 1f / 30, false, 0, stalking: 1);
                Assert.That(Quaternion.Angle(before, head.localRotation), Is.GreaterThan(15));
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void RuntimeFetchReachesScreenEdgeAndClearsOnInterrupt()
        {
            var root = new GameObject("捡球回归");
            var controller = root.AddComponent<DesktopPetController>();
            controller.enabled = false;
            var pet = new PrototypePet();
            var camera = new GameObject("捡球测试相机").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 80, -80); camera.transform.LookAt(Vector3.zero);
            var motion = new PetMotion(new DesktopRect(0, 0, 1000, 800), 88);
            motion.DragTo(new Point2(300, 300));
            Set(controller, "pet", pet); Set(controller, "petCamera", camera);
            Set(controller, "motion", motion); Set(controller, "behavior", new PetBehavior(new System.Random(1)));
            var fetch = (PetMouseFetch)Get(controller, "mouseFetch");
            try
            {
                Tick(controller, new Vector2(420, 300));
                Tick(controller, new Vector2(1400, 300));
                Tick(controller, new Vector2(1400, 300));
                Tick(controller, new Vector2(1400, 300));
                Assert.That(fetch.Ball.X, Is.EqualTo(912), "球裁剪到真实可达工作区");
                for (int i = 0; i < 40 && fetch.Phase != MouseFetchPhase.Spiraling; i++)
                    Tick(controller, new Vector2(1400, 300));
                Assert.That(fetch.Phase, Is.EqualTo(MouseFetchPhase.Spiraling), "鼠标在屏幕之外也可完成返回");
                Call(controller, "UpdatePresentation", new Vector2(1400, 300), motion.Position, .1f);
                var ball = (GameObject)Get(controller, "fetchBall");
                Assert.That(Vector3.Distance(ball.transform.position, pet.CarryAnchor), Is.LessThan(.001f));
                Call(controller, "InterruptAutonomy");
                Assert.That(fetch.Active, Is.False);
                Assert.That(ball.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(pet.Root.gameObject);
                Object.DestroyImmediate(camera.gameObject);
            }
        }
        private static object Get(object value, string field) { return value.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value); }
        private static void Set(object value, string field, object data) { value.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, data); }
        private static void Call(object value, string method, params object[] args) { value.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(value, args); }
        private static void Tick(DesktopPetController value, Vector2 pointer) { Call(value, "TickMouseFetch", pointer, .1f, false); }
    }
}

