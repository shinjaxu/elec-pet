using System;

namespace ElecPet.Core
{
    public enum PetLifeState { Free, ApproachingOwner, RestingNearOwner }

    // 只处理活动时间与生活阶段；窗口输入、模型移动和文件保存由运行时编排。
    public sealed class PetLife
    {
        private DesktopRect bounds;
        private Point2 ownerAnchor;
        private double clock, phaseTime, quietTime, ownerAt;
        public PetLifeState State { get; private set; }
        public Point2 Target { get; private set; }
        public bool CanApproachOwner { get { return clock >= ownerAt; } }
        public double Progress { get { return Math.Min(1, phaseTime / 8); } }

        public PetLife(DesktopRect bounds) { this.bounds = bounds; }

        public void Tick(double seconds, PetBehavior behavior, Point2 position, Point2 pointer, bool pointerQuiet, bool mouseBusy)
        {
            if (seconds <= 0) return;
            clock += seconds;
            quietTime = pointerQuiet && !mouseBusy ? quietTime + seconds : 0;
            if (State == PetLifeState.ApproachingOwner || State == PetLifeState.RestingNearOwner)
            {
                // 锁定稳定鼠标的位置，不追着移动的光标跑，也不在点击时继续靠近。
                if (mouseBusy || !pointerQuiet || Distance(pointer, ownerAnchor) > 12)
                { behavior.StopRest(); Interrupt(); return; }
            }
            if (State != PetLifeState.Free)
            {
                phaseTime += seconds;
                if (State == PetLifeState.ApproachingOwner)
                {
                    if (Distance(position, Target) <= 4)
                    {
                        Enter(PetLifeState.RestingNearOwner);
                        behavior.Rest(8);
                    }
                }
                else if (State == PetLifeState.RestingNearOwner)
                {
                    if (phaseTime >= 8) { behavior.StopRest(); Interrupt(); }
                }
                return;
            }
            if (behavior.State != PetState.Wander || mouseBusy) return;
            if (behavior.Energy < .35) return;
            double pointerDistance = Distance(position, pointer);
            if (behavior.Relationship == PetRelationship.Trusted && quietTime >= 4 - .00001 && clock >= ownerAt
                && pointerDistance >= 120 && pointerDistance <= 260)
            {
                ownerAnchor = pointer;
                Target = Clamp(new Point2(pointer.X + (position.X - pointer.X) / pointerDistance * 100,
                    pointer.Y + (position.Y - pointer.Y) / pointerDistance * 100));
                // 边缘或极小工作区无法保持距离时，不反复尝试不可达的位置。
                ownerAt = clock + 90;
                if (Distance(Target, pointer) < 90) return;
                Enter(PetLifeState.ApproachingOwner);
                return;
            }
        }

        public void Interrupt()
        {
            if (State == PetLifeState.ApproachingOwner || State == PetLifeState.RestingNearOwner) ownerAt = Math.Max(ownerAt, clock + 90);
            State = PetLifeState.Free;
            phaseTime = quietTime = 0;
        }

        public void Resize(DesktopRect area)
        {
            bounds = area;
            Interrupt();
        }

        private Point2 Clamp(Point2 point) { return DesktopGeometry.Clamp(point, bounds, 88); }
        private void Enter(PetLifeState state) { State = state; phaseTime = 0; }
        private static double Distance(Point2 a, Point2 b) { return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)); }
    }
}
