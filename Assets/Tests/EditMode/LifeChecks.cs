using System;
using ElecPet.Core;
namespace ElecPet.Tests
{
    public static class LifeChecks
    {
        public static int Run()
        {
            int count = 0;
            var area = new DesktopRect(0, 0, 1000, 800);
            var pet = new PetBehavior(new Random(1), .5);
            var life = new PetLife(area);
            var position = new Point2(100, 300);
            var pointer = new Point2(300, 300);
            for (int i = 0; i < 40; i++) life.Tick(.1, pet, position, pointer, true, false);
            Check(life.State == PetLifeState.ApproachingOwner, "信任后安静靠近", ref count);
            Check(Math.Abs(life.Target.X - 200) < .01, "靠近保留安全距离", ref count);
            life.Tick(.1, pet, life.Target, pointer, true, false);
            Check(life.State == PetLifeState.RestingNearOwner, "靠近后原地休息，不依赖小窝", ref count);
            double progress = life.Progress;
            life.Tick(0, pet, life.Target, pointer, true, false);
            Check(life.Progress == progress, "暂停不推进休息", ref count);
            life.Tick(.1, pet, life.Target, pointer, true, true);
            Check(life.State == PetLifeState.Free && pet.State == PetState.Wander, "输入中断休息", ref count);
            for (int i = 0; i < 100; i++) life.Tick(.1, pet, position, pointer, true, false);
            Check(life.State == PetLifeState.Free, "亲近保留冷却", ref count);
            life = new PetLife(area);
            var fresh = new PetBehavior(new Random(1));
            for (int i = 0; i < 1000; i++) life.Tick(.1, fresh, position, pointer, false, false);
            Check(life.State == PetLifeState.Free, "自由活动不再寻找窝或玩具", ref count);
            life = new PetLife(area);
            for (int i = 0; i < 40; i++) life.Tick(.1, pet, position, pointer, true, false);
            life.Resize(new DesktopRect(0, 0, 100, 80));
            Check(life.State == PetLifeState.Free, "缩放中断旧亲近目标", ref count);
            return count;
        }
        private static void Check(bool value, string message, ref int count)
        { if (!value) throw new InvalidOperationException(message); count++; }
    }
}
