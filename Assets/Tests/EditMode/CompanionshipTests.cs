using System;
using System.IO;
using ElecPet.Core;
using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ElecPet.Tests
{
    public sealed class CompanionshipTests
    {
        private string directory;
        private readonly DesktopRect area = new DesktopRect(100, 50, 1000, 700);

        [SetUp]
        public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "ElecPetUnity-" + Guid.NewGuid().ToString("N")); }
        [TearDown]
        public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test]
        public void LegacyObjectFieldsDoNotPreventRelationshipRecovery()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "pet.json"), "{\"Version\":1,\"Familiarity\":0.4,\"HeadPreference\":0.7,\"HomeX\":0.2,\"BallX\":0.4}");
            var companion = Create();
            Assert.That(companion.Data.Familiarity, Is.EqualTo(.4));
            companion.Save(0, new PetBehavior(new System.Random(1), .5, .7), true);
            string saved = File.ReadAllText(Path.Combine(directory, "pet.json"));
            Assert.That(saved, Does.Not.Contain("HomeX").And.Not.Contain("BallX"));
            Assert.That(Create().Data.Familiarity, Is.EqualTo(.5));
        }

        [Test]
        public void AutosaveRunsWhileLifeIsFrozenAndOnlyWhenDirty()
        {
            var companion = Create();
            var pet = new PetBehavior(new System.Random(1));
            companion.Save(0, pet, true);
            pet.Touch(PetTouch.Head, true);
            companion.Save(29, pet);
            Assert.That(Create().Data.Familiarity, Is.EqualTo(0));
            companion.Save(30, pet);
            Assert.That(Create().Data.Familiarity, Is.EqualTo(.006));
            var file = Path.Combine(directory, "pet.json");
            DateTime written = File.GetLastWriteTimeUtc(file);
            companion.Save(60, pet);
            Assert.That(File.GetLastWriteTimeUtc(file), Is.EqualTo(written));
            Assert.That(companion.Life.State, Is.EqualTo(PetLifeState.Free));
        }

        [Test]
        public void FailedSaveRetainsDirtyProgressForNextPeriod()
        {
            var companion = Create();
            var pet = new PetBehavior(new System.Random(1), .5);
            companion.Save(0, pet, true);
            Directory.CreateDirectory(Path.Combine(directory, "pet.json.tmp"));
            pet.Touch(PetTouch.Head, true);
            Assert.That(companion.Save(1, pet, true), Is.False);
            Directory.Delete(Path.Combine(directory, "pet.json.tmp"));
            Assert.That(companion.Save(30, pet), Is.False, "失败后等待下个周期");
            Assert.That(companion.Save(31, pet), Is.True);
            Assert.That(Create().Data.Familiarity, Is.EqualTo(.506).Within(.00001));
        }

        private PetCompanionship Create()
        { return new PetCompanionship(directory, area, PetSaveData.Default, (message, exception) => { }); }

        [TestCase(30)]
        [TestCase(60)]
        public void FollowingStopsWithoutReturningAndRechasing(int framesPerSecond)
        {
            var host = new GameObject("跟随停止距离检查");
            var controller = host.AddComponent<DesktopPetController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(DesktopPetController);
            try
            {
                var behavior = new PetBehavior(new System.Random(1));
                // 消耗扑食机会，单独检查普通跟随到达停止距离后的连续运动。
                behavior.TryHunt(130); behavior.Touch(PetTouch.Head, true); behavior.Tick(2);
                var motion = new PetMotion(new DesktopRect(0, 0, 1000, 800), 88);
                motion.DragTo(new Point2(300, 300));
                type.GetField("behavior", flags).SetValue(controller, behavior);
                type.GetField("motion", flags).SetValue(controller, motion);
                var pointer = new Vector2(450, 300);
                float dt = 1f / framesPerSecond;
                var update = type.GetMethod("UpdateAutonomy", flags);
                for (int i = 0; i < framesPerSecond * 2; i++) update.Invoke(controller, new object[] { pointer, dt, false });
                Vector2 stopped = new Vector2((float)motion.Position.X, (float)motion.Position.Y);
                Assert.That(Vector2.Distance(stopped, pointer), Is.EqualTo(90).Within(.01), "普通跟随应准确停在最近距离");
                for (int i = 0; i < framesPerSecond * 2; i++)
                {
                    update.Invoke(controller, new object[] { pointer, dt, false });
                    Assert.That(Vector2.Distance(stopped, new Vector2((float)motion.Position.X, (float)motion.Position.Y)),
                        Is.LessThan(.01), "鼠标静止时不能在追逐和归边之间反复切换");
                }
                // 鼠标离开跟随范围后仍应恢复归边，而非永远钉在停止位置。
                for (int i = 0; i < 30; i++) update.Invoke(controller, new object[] { new Vector2(900, 600), dt, false });
                Assert.That(Vector2.Distance(stopped, new Vector2((float)motion.Position.X, (float)motion.Position.Y)), Is.GreaterThan(1));
            }
            finally
            {
                type.GetField("behavior", flags).SetValue(controller, null);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HuntLandingKeepsStillNearAStationaryPointer()
        {
            var host = new GameObject("扑食落地停止检查");
            var controller = host.AddComponent<DesktopPetController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(DesktopPetController);
            try
            {
                var behavior = new PetBehavior(new System.Random(1));
                var motion = new PetMotion(new DesktopRect(0, 0, 1000, 800), 88);
                motion.DragTo(new Point2(300, 300));
                type.GetField("behavior", flags).SetValue(controller, behavior);
                type.GetField("motion", flags).SetValue(controller, motion);
                var pointer = new Vector2(430, 300);
                var update = type.GetMethod("UpdateAutonomy", flags);
                update.Invoke(controller, new object[] { pointer, 1f / 30, false });
                Assert.That(behavior.State, Is.EqualTo(PetState.Hunting));
                for (int i = 0; i < 90; i++)
                {
                    behavior.Tick(1.0 / 30, false);
                    update.Invoke(controller, new object[] { pointer, 1f / 30, false });
                }
                Vector2 stopped = new Vector2((float)motion.Position.X, (float)motion.Position.Y);
                Assert.That(Vector2.Distance(stopped, pointer), Is.EqualTo(65).Within(.01));
                for (int i = 0; i < 60; i++)
                {
                    behavior.Tick(1.0 / 30, false);
                    update.Invoke(controller, new object[] { pointer, 1f / 30, false });
                    Assert.That(Vector2.Distance(stopped, new Vector2((float)motion.Position.X, (float)motion.Position.Y)), Is.LessThan(.01));
                }
            }
            finally
            {
                type.GetField("behavior", flags).SetValue(controller, null);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FamiliarPetLeansItsHeadIntoGentleTouch()
        {
            var pet = new PrototypePet();
            try
            {
                float clock = 0;
                var head = pet.Root.Find("躯干/注视点");
                for (int i = 0; i < 30; i++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 1, PetState.Petted, .5f, PetTouch.Head, false);
                float initialZ = head.localPosition.z;
                for (int i = 0; i < 30; i++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 1, PetState.Petted, .5f, PetTouch.Head, true);
                Assert.That(head.localPosition.z, Is.LessThan(initialZ - .02f), "熟悉后摸头需有可见的主动迎合");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void TrustedApproachWaitsForQuietPointerBeforeHuntingOrWandering()
        {
            var host = new GameObject("主动亲近流程检查");
            var controller = host.AddComponent<DesktopPetController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(DesktopPetController);
            try
            {
                var pet = new PetBehavior(new System.Random(1), .5);
                var companion = Create();
                var motion = new PetMotion(area, 88);
                motion.DragTo(new Point2(300, 300));
                type.GetField("behavior", flags).SetValue(controller, pet);
                type.GetField("companionship", flags).SetValue(controller, companion);
                type.GetField("motion", flags).SetValue(controller, motion);
                var pointer = new Vector2(450, 300);
                type.GetField("lastPointer", flags).SetValue(controller, pointer);
                for (int i = 0; i < 40; i++)
                    type.GetMethod("UpdateAutonomy", flags).Invoke(controller, new object[] { pointer, .1f, false });
                Assert.That(companion.Life.State, Is.EqualTo(PetLifeState.ApproachingOwner), "原扑食或巡游不能在等待鼠标稳定时抢走位置");
                Assert.That(pet.State, Is.EqualTo(PetState.Wander));
            }
            finally
            {
                type.GetField("behavior", flags).SetValue(controller, null);
                type.GetField("companionship", flags).SetValue(controller, null);
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActualUpdateSavesDirtyProgressWhilePausedOrHidden(bool hidden)
        {
            var host = new GameObject("暂停隐藏保存流程检查");
            var controller = host.AddComponent<DesktopPetController>();
            var model = new PrototypePet();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(DesktopPetController);
            try
            {
                var pet = new PetBehavior(new System.Random(1));
                var companion = Create();
                companion.Save(0, pet, true);
                var trusted = new PetBehavior(new System.Random(1), .5);
                for (int i = 0; i < 40; i++) companion.Life.Tick(.1, trusted, new Point2(300, 300), new Point2(450, 300), true, false);
                Assert.That(companion.Life.State, Is.EqualTo(PetLifeState.ApproachingOwner));
                double progress = companion.Life.Progress;
                pet.Touch(PetTouch.Head, true);
                typeof(PetCompanionship).GetField("saveAt", flags).SetValue(companion, -1d);
                type.GetField("behavior", flags).SetValue(controller, pet);
                type.GetField("companionship", flags).SetValue(controller, companion);
                type.GetField("motion", flags).SetValue(controller, new PetMotion(area, 88));
                type.GetField("pet", flags).SetValue(controller, model);
                type.GetField("petCamera", flags).SetValue(controller, host.AddComponent<Camera>());
                type.GetField("width", flags).SetValue(controller, Screen.width);
                type.GetField("height", flags).SetValue(controller, Screen.height);
                type.GetField(hidden ? "hidden" : "paused", flags).SetValue(controller, true);
                type.GetMethod("Update", flags).Invoke(controller, null);
                Assert.That(Create().Data.Familiarity, Is.EqualTo(.006));
                Assert.That(companion.Life.Progress, Is.EqualTo(progress));
                Assert.That(companion.Life.State, Is.EqualTo(PetLifeState.ApproachingOwner));
            }
            finally
            {
                type.GetField("behavior", flags).SetValue(controller, null);
                type.GetField("companionship", flags).SetValue(controller, null);
                Object.DestroyImmediate(model.Root.gameObject); Object.DestroyImmediate(host);
            }
        }
    }
}
