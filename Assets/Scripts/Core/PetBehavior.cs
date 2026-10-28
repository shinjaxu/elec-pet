using System;

namespace ElecPet.Core
{
    public enum PetState { Wander, Petted, Guarding, Startled, Carried, Recovering, Greeting, Curious, Stretching, Grooming, Drowsy, Lying, Alert, Hunting }
    public enum PetTouch { Head, Body, Wing, Tail }
    public enum PetRelationship { New, Familiar, Trusted }

    // 行为只使用活动时间；暂停和隐藏时不推进，避免恢复显示后补播过期动作。
    public sealed class PetBehavior
    {
        private readonly Random random;
        private double clock, remaining, duration, idleRemaining, lastPoke = -10, lastBond = -10, lastInteraction;
        private int pokes, lastIdle = -1;
        private double lastHunt = -20;
        public PetState State { get; private set; }
        public PetTouch TouchPart { get; private set; }
        public double Energy { get; private set; }
        public double Mood { get; private set; }
        public double Familiarity { get; private set; }
        public double HeadPreference { get; private set; }
        public PetRelationship Relationship { get { return Familiarity < .1 ? PetRelationship.New : Familiarity < .35 ? PetRelationship.Familiar : PetRelationship.Trusted; } }
        public double Progress { get { return duration > 0 ? 1 - remaining / duration : 0; } }
        public bool IsIdleAction { get { return State >= PetState.Curious && State <= PetState.Alert; } }

        public PetBehavior(Random random, double familiarity = 0, double headPreference = 0)
        {
            this.random = random;
            Energy = 1;
            Mood = .7;
            Familiarity = Clamp(familiarity);
            HeadPreference = Clamp(headPreference);
            ScheduleIdle();
        }

        public void Tick(double seconds, bool allowIdle = true)
        {
            if (seconds <= 0) return;
            clock += seconds;
            Energy = Clamp(Energy + seconds * (State == PetState.Drowsy || State == PetState.Lying ? .07 : State == PetState.Carried ? -.004 : -.001));
            // 心情自行回到平和状态，不因用户暂时没有照顾而持续恶化。
            Mood += Math.Sign(.7 - Mood) * Math.Min(Math.Abs(.7 - Mood), seconds * .004);
            if (State == PetState.Carried) return;
            if (State != PetState.Wander)
            {
                remaining -= seconds;
                if (remaining <= 0) { Enter(PetState.Wander, 0); ScheduleIdle(); }
                return;
            }
            if (!allowIdle) return;
            idleRemaining -= seconds;
            if (idleRemaining > 0) return;
            // 从剩余动作中抽取，避免刚做完同一种片段又重复播放。
            double[] weights = { 1 + Mood + Familiarity, .7 + Energy, .8, .25 + (1 - Energy) * 5, .7 + (1 - Energy) * 3, .8 };
            if (lastIdle >= 0) weights[lastIdle] = 0;
            double total = 0;
            foreach (double weight in weights) total += weight;
            double draw = random.NextDouble() * total;
            int choice = 0;
            while (choice < weights.Length - 1 && draw >= weights[choice]) draw -= weights[choice++];
            lastIdle = choice;
            Enter((PetState)((int)PetState.Curious + choice), choice == 3 || choice == 4 ? 6 : 3.5);
        }

        public void Touch(PetTouch part, bool gentle)
        {
            if (State == PetState.Carried || State == PetState.Startled || State == PetState.Recovering) return;
            bool continuingGuard = State == PetState.Guarding && TouchPart == part;
            TouchPart = part;
            lastInteraction = clock;
            ScheduleIdle();
            if (gentle)
            {
                pokes = 0;
                // 持续抚摸每两秒计一次，熟悉度不依赖渲染帧率。
                if (clock - lastBond >= 2)
                {
                    lastBond = clock;
                    Familiarity = Clamp(Familiarity + .006);
                    Mood = Clamp(Mood + .04);
                    if (part == PetTouch.Head) HeadPreference = Clamp(HeadPreference + .025);
                }
                // 同部位持续划动时保留护翼或卷尾进度，避免每帧重置后动作始终停在起点。
                if (!continuingGuard)
                    Enter(part == PetTouch.Tail || part == PetTouch.Wing ? PetState.Guarding : PetState.Petted, 1.4);
                return;
            }
            pokes = clock - lastPoke <= 1.5 ? pokes + 1 : 1;
            lastPoke = clock;
            if (pokes >= 3) { pokes = 0; Mood = Clamp(Mood - .18); Enter(PetState.Startled, 2.8); }
            else Enter(part == PetTouch.Tail || part == PetTouch.Wing ? PetState.Guarding : PetState.Curious, 1);
        }

        public void PickUp() { pokes = 0; lastInteraction = clock; Enter(PetState.Carried, 0); }
        public void PutDown() { lastInteraction = clock; Enter(PetState.Recovering, 1.6); ScheduleIdle(); }

        // 文件悬停吸引注意后结束原动作，避免进食结束又续播先前锁定的扑跃轨迹。
        public void AttendFood() { pokes = 0; lastInteraction = clock; Enter(PetState.Wander, 0); ScheduleIdle(); }

        // 亲近流程在到达用户附近后才调用，路途中不提前恢复精力或播放卧躺。
        public void Rest(double seconds) { Enter(PetState.Lying, seconds); }
        public void StopRest()
        {
            if (State == PetState.Lying) { Enter(PetState.Wander, 0); ScheduleIdle(); }
        }

        public bool TryHunt(double pointerDistance)
        {
            // 只在自由活动时扑向附近鼠标；使用活动时间冷却，暂停不消耗冷却时间。
            if (State != PetState.Wander || Energy < .25 || pointerDistance < 95 || pointerDistance > 175 || clock - lastHunt < 18)
                return false;
            lastHunt = clock;
            lastInteraction = clock;
            Enter(PetState.Hunting, 2.4);
            ScheduleIdle();
            return true;
        }

        public void NoticePointer()
        {
            if (Familiarity < .1 || clock - lastInteraction < 60) return;
            if (State != PetState.Wander && !IsIdleAction) return;
            lastInteraction = clock;
            Enter(PetState.Greeting, 2);
        }

        private void Enter(PetState state, double seconds)
        {
            State = state;
            duration = remaining = seconds;
        }

        private void ScheduleIdle() { idleRemaining = 10 + random.NextDouble() * 12; }
        private static double Clamp(double value) { return Math.Max(0, Math.Min(1, value)); }
    }
}
