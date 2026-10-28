using System;
using ElecPet.Core;

namespace ElecPet.Tests
{
    public static class BehaviorChecks
    {
        public static int Run()
        {
            int count = 0;
            double[] levels = { 0, .099, .1, .349, .35, 1 };
            PetRelationship[] stages = { PetRelationship.New, PetRelationship.New, PetRelationship.Familiar,
                PetRelationship.Familiar, PetRelationship.Trusted, PetRelationship.Trusted };
            for (int i = 0; i < levels.Length; i++)
                Check(new PetBehavior(new Random(1), levels[i]).Relationship == stages[i], "关系阶段由熟悉度推导", ref count);
            var paced = new PetBehavior(new Random(1));
            paced.Touch(PetTouch.Head, true);
            for (int i = 0; i < 59; i++) { paced.Tick(1.0 / 30); paced.Touch(PetTouch.Head, true); }
            Check(Math.Abs(paced.Familiarity - .006) < .00001, "两秒内重复抚摸不重复累计关系", ref count);
            Check(new PetBehavior(new Random(1), .35).Relationship == PetRelationship.Trusted, "重启不惩罚关系", ref count);
            var pet = new PetBehavior(new Random(17));
            pet.Touch(PetTouch.Head, true);
            Check(pet.State == PetState.Petted, "温柔摸头会迎合", ref count);
            pet.Tick(3);
            Check(pet.State == PetState.Wander, "抚摸反馈结束后恢复活动", ref count);
            pet.Touch(PetTouch.Tail, false);
            Check(pet.State == PetState.Guarding, "触碰尾巴会护住尾巴", ref count);
            pet.Tick(2);
            pet.Touch(PetTouch.Body, false);
            pet.Tick(.2);
            pet.Touch(PetTouch.Body, false);
            pet.Tick(.2);
            pet.Touch(PetTouch.Body, false);
            Check(pet.State == PetState.Startled, "短时间三次戳弄会躲闪", ref count);
            pet.Touch(PetTouch.Head, true);
            Check(pet.State == PetState.Startled, "躲闪期间不会立即重新追逐或迎合", ref count);
            pet.Tick(4);
            Check(pet.State == PetState.Wander, "躲闪后自行恢复", ref count);
            pet.Touch(PetTouch.Body, false);
            pet.Tick(2);
            pet.Touch(PetTouch.Body, false);
            pet.Tick(2);
            pet.Touch(PetTouch.Body, false);
            Check(pet.State != PetState.Startled, "间隔较长的触碰不会累积为连续戳弄", ref count);
            pet.PickUp();
            Check(pet.State == PetState.Carried, "提起会中断当前动作", ref count);
            pet.Tick(30);
            Check(pet.State == PetState.Carried, "提起期间不会切换随机动作", ref count);
            pet.PutDown();
            Check(pet.State == PetState.Recovering, "放下后先整理姿势", ref count);
            pet.Tick(2);
            Check(pet.State == PetState.Wander, "整理完成后恢复巡游", ref count);
            PetState previous = PetState.Wander;
            int seen = 0;
            for (int i = 0; i < 1000; i++)
            {
                pet.Tick(.25);
                if (!pet.IsIdleAction) continue;
                Check(pet.State != previous, "随机片段不连续重复", ref count);
                previous = pet.State;
                seen++;
                pet.Touch(PetTouch.Head, true);
                Check(pet.State == PetState.Petted, "用户互动能打断随机片段", ref count);
                pet.Tick(3);
                Check(pet.State == PetState.Wander, "互动后保留随机行为冷却", ref count);
                pet.Tick(0);
                Check(pet.State == PetState.Wander, "零时间不推进动作", ref count);
            }
            Check(seen >= 5, "空闲期间会主动做小动作", ref count);
            var bonded = new PetBehavior(new Random(7));
            double startingEnergy = bonded.Energy;
            bonded.PickUp();
            bonded.Tick(20);
            Check(bonded.Energy < startingEnergy, "持续玩耍会消耗精力", ref count);
            bonded.PutDown();
            bonded.Tick(2);
            double startingMood = bonded.Mood;
            for (int i = 0; i < 40; i++)
            {
                bonded.Touch(PetTouch.Head, true);
                bonded.Tick(2);
            }
            Check(bonded.Familiarity > .1, "相处互动逐渐增加熟悉度", ref count);
            Check(bonded.HeadPreference > .4, "温柔摸头会形成偏好", ref count);
            Check(bonded.Mood > startingMood, "温柔互动改善心情", ref count);
            double savedBond = bonded.Familiarity;
            double savedPreference = bonded.HeadPreference;
            var restored = new PetBehavior(new Random(7), savedBond, savedPreference);
            Check(Math.Abs(restored.Familiarity - savedBond) < .001 && Math.Abs(restored.HeadPreference - savedPreference) < .001,
                "重新启动保留熟悉度与偏好", ref count);
            restored.Tick(61);
            restored.NoticePointer();
            Check(restored.State == PetState.Greeting, "熟悉后长时间无互动会主动招呼", ref count);
            restored.Tick(3);
            restored.NoticePointer();
            Check(restored.State == PetState.Wander, "招呼有冷却，不会持续打扰", ref count);
            restored.Touch(PetTouch.Body, false);
            restored.Touch(PetTouch.Body, false);
            restored.Touch(PetTouch.Body, false);
            double upsetMood = restored.Mood;
            restored.Tick(30);
            Check(restored.Mood > upsetMood, "短暂不高兴能够自行恢复", ref count);
            var tired = new PetBehavior(new Random(4));
            tired.PickUp();
            tired.Tick(300);
            tired.PutDown();
            tired.Tick(2);
            for (int i = 0; i < 1000 && tired.State != PetState.Drowsy; i++) tired.Tick(.25);
            Check(tired.State == PetState.Drowsy, "疲倦后会选择打瞌睡", ref count);
            double tiredEnergy = tired.Energy;
            tired.Tick(2);
            Check(tired.Energy > tiredEnergy, "休息能够恢复精力", ref count);
            var wingTouch = new PetBehavior(new Random(3));
            wingTouch.Touch(PetTouch.Wing, true);
            for (int i = 0; i < 20; i++)
            {
                wingTouch.Tick(1.0 / 30);
                wingTouch.Touch(PetTouch.Wing, true);
            }
            Check(wingTouch.State == PetState.Guarding && wingTouch.Progress > .4,
                "持续划过翅膀时反馈仍会推进，不被每帧触碰重置", ref count);
            var animal = new PetBehavior(new Random(12));
            bool lying = false, alert = false;
            for (int i = 0; i < 4000; i++)
            {
                animal.Tick(.25);
                lying |= animal.State.ToString() == "Lying";
                alert |= animal.State.ToString() == "Alert";
            }
            Check(lying && alert, "自主活动包含卧躺和警戒", ref count);
            var hunter = new PetBehavior(new Random(2));
            Check(!hunter.TryHunt(30) && !hunter.TryHunt(220), "扑食只针对附近且没有贴身的鼠标", ref count);
            Check(hunter.TryHunt(130), "鼠标进入范围会触发扑食", ref count);
            Check(hunter.State == PetState.Hunting, "扑食具有独立状态", ref count);
            Check(!hunter.IsIdleAction, "扑食不属于随机空闲片段", ref count);
            hunter.Tick(.8);
            Check(hunter.Progress > .3 && hunter.State == PetState.Hunting, "扑食蓄势阶段不会立即结束", ref count);
            hunter.Touch(PetTouch.Head, true);
            Check(hunter.State == PetState.Petted, "抚摸能够打断扑食", ref count);
            hunter.Tick(3);
            Check(!hunter.TryHunt(130), "扑食有冷却，避免反复扑向鼠标", ref count);
            hunter.PickUp();
            Check(!hunter.TryHunt(130), "提起期间不会触发扑食", ref count);
            return count;
        }

        private static void Check(bool success, string label, ref int count)
        {
            if (!success) throw new InvalidOperationException(label);
            count++;
        }
    }
}
