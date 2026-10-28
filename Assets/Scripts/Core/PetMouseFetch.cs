using System;
namespace ElecPet.Core
{
    public enum MouseFetchPhase { Idle, Stalking, Fetching, PickingUp, Returning, Spiraling, Dropping }

    // 手势只在近距离蓄势后识别；活动过程使用活动时间，不读取或操作桌面内容。
    public sealed class PetMouseFetch
    {
        private Point2 previous, strokeStart, previousPet;
        private bool sampled, throwing;
        private double elapsed, still, cooldown, flightProgress, stalled;
        public MouseFetchPhase Phase { get; private set; }
        public Point2 Ball { get; private set; }
        public Point2 Target { get; private set; }
        public Point2 Landing { get; private set; }
        public bool Active { get { return Phase != MouseFetchPhase.Idle; } }
        public bool BallVisible { get { return Phase >= MouseFetchPhase.Fetching; } }
        public bool Carrying { get { return Phase >= MouseFetchPhase.Returning && (Phase != MouseFetchPhase.Dropping || elapsed < .45); } }
        public double FlightAngle { get { return flightProgress * Math.PI * 4; } }
        public double FlightProgress { get { return flightProgress; } }
        public double Bend { get { return Phase == MouseFetchPhase.PickingUp ? Math.Sin(Math.Min(1, elapsed / .35) * Math.PI) : Phase == MouseFetchPhase.Dropping ? Math.Sin(Math.Min(1, elapsed / .65) * Math.PI) : 0; } }
        public double BallOpacity { get { return Phase == MouseFetchPhase.Dropping ? Math.Min(1, Math.Max(0, (1 - elapsed) / .35)) : 1; } }
        private static double Distance(Point2 a, Point2 b) { double x = a.X - b.X, y = a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
        private void Enter(MouseFetchPhase phase) { Phase = phase; elapsed = 0; }
        public void Cancel()
        {
            if (Active) cooldown = 1;
            Enter(MouseFetchPhase.Idle); throwing = sampled = false; still = flightProgress = stalled = 0;
        }
        public void Tick(double dt, Point2 pet, Point2 pointer, bool available)
        {
            if (!available) { Cancel(); return; }
            if (dt <= 0) return;
            cooldown = Math.Max(0, cooldown - dt);
            double speed = sampled ? Distance(pointer, previous) / dt : 0;
            // 错位屏幕之间可能没有可直达通道；无法前进时退出，不能永久占用自主行为。
            stalled = (Phase == MouseFetchPhase.Fetching || Phase == MouseFetchPhase.Returning)
                && Distance(pet, previousPet) < .01 ? stalled + dt : 0;
            previousPet = pet;
            if (stalled >= 1.5) { Cancel(); return; }
            previous = pointer; sampled = true; elapsed += dt;
            if (Phase == MouseFetchPhase.Idle)
            {
                if (cooldown <= 0 && Distance(pet, pointer) <= 175) { Enter(MouseFetchPhase.Stalking); strokeStart = pointer; }
                return;
            }
            if (Phase == MouseFetchPhase.Stalking)
            {
                if (!throwing && speed >= 850) { throwing = true; still = 0; elapsed = 0; }
                if (throwing)
                {
                    still = speed < 140 ? still + dt : 0;
                    if (still >= .12 && Distance(strokeStart, pointer) >= 90)
                    {
                        Ball = Target = pointer; throwing = false; Enter(MouseFetchPhase.Fetching);
                    }
                    else if (elapsed > .8) Cancel();
                }
                else if (Distance(pet, pointer) > 220) Cancel();
                else strokeStart = pointer;
                return;
            }
            if (Phase == MouseFetchPhase.Fetching && Distance(pet, Ball) <= 12) Enter(MouseFetchPhase.PickingUp);
            else if (Phase == MouseFetchPhase.PickingUp && elapsed >= .35) Enter(MouseFetchPhase.Returning);
            else if (Phase == MouseFetchPhase.Returning)
            {
                Target = pointer;
                if (Distance(pet, pointer) <= 75)
                {
                    // 以宠物当前安全位置为落点；鼠标继续移动也不拉走已开始的飞行。
                    Landing = pet; flightProgress = 0; Enter(MouseFetchPhase.Spiraling);
                }
            }
            else if (Phase == MouseFetchPhase.Spiraling)
            {
                flightProgress = Math.Min(1, elapsed / 2.4);
                if (flightProgress >= 1) Enter(MouseFetchPhase.Dropping);
            }
            else if (Phase == MouseFetchPhase.Dropping && elapsed >= 1) { Cancel(); cooldown = 2; }
        }
        // 安全目标由运行层按真实屏幕裁剪，避免屏幕边缘的球或鼠标永远不可达。
        public void ConstrainTargets(Func<Point2, Point2> clamp)
        {
            if (Phase == MouseFetchPhase.Fetching) Ball = Target = clamp(Ball);
            if (Phase == MouseFetchPhase.Returning) Target = clamp(Target);
        }
    }
}
