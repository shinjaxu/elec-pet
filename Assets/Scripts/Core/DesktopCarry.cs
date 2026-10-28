using System;

namespace ElecPet.Core
{
    public enum CarryPhase { Idle, Approach, PickUp, Transport, PutDown, Complete }

    // 时间仅来自已确认的活动帧；等待 Shell、暂停或键盘输入时不累计，鼠标不影响空闲门槛。
    public sealed class DesktopCarry
    {
        private DesktopMove move;
        private DesktopRect bounds;
        private Point2 offset, petStart, petEnd, iconStart, iconEnd, correction;
        private double elapsed, duration, gripStart;
        public CarryPhase Phase { get; private set; }
        public Point2 PetPosition { get; private set; }
        public Point2 IconPosition { get; private set; }
        public double Grip { get; private set; }
        public double Progress { get { return duration > 0 ? Math.Min(1, elapsed / duration) : 0; } }
        public double Bend { get { return Phase == CarryPhase.PickUp || Phase == CarryPhase.PutDown ? Math.Sin(Progress * Math.PI) : 0; } }
        public bool WantsWrite { get { return Phase >= CarryPhase.PickUp; } }

        public void Begin(DesktopMove move, Point2 petPosition, DesktopRect petBounds, Point2 carryOffset)
        {
            this.move = move; bounds = petBounds; offset = carryOffset;
            PetPosition = petPosition; IconPosition = move.From; Grip = 0;
            Enter(CarryPhase.Approach);
        }

        public void Tick(double dt, double idleSeconds, bool enabled)
        {
            if (!enabled || idleSeconds < 30 || dt <= 0 || Phase == CarryPhase.Idle || Phase == CarryPhase.Complete) return;
            elapsed = Math.Min(duration, elapsed + dt);
            double t = Progress, smooth = t * t * (3 - 2 * t);
            PetPosition = Lerp(petStart, petEnd, smooth);
            if (Phase == CarryPhase.PickUp || Phase == CarryPhase.Transport)
                iconEnd = new Point2(petEnd.X + offset.X, petEnd.Y + offset.Y);
            IconPosition = Lerp(iconStart, iconEnd, smooth);
            if (Phase == CarryPhase.Transport)
            {
                double blend = Math.Min(1, elapsed / .6); blend = blend * blend * (3 - 2 * blend);
                IconPosition = new Point2(PetPosition.X + offset.X + correction.X * (1 - blend),
                    PetPosition.Y + offset.Y + correction.Y * (1 - blend));
            }
            if (Phase == CarryPhase.PickUp) Grip = gripStart + (1 - gripStart) * smooth;
            if (Phase == CarryPhase.PutDown) Grip = gripStart * (1 - smooth);
            if (elapsed >= duration - .000001)
            {
                PetPosition = petEnd; IconPosition = iconEnd;
                if (Phase == CarryPhase.PickUp) Grip = 1;
                if (Phase == CarryPhase.PutDown) Grip = 0;
                Enter(Phase + 1);
            }
        }

        public void Rebase(Point2 petPosition, Point2 actualIconPosition)
        {
            PetPosition = petPosition; IconPosition = actualIconPosition;
            if (Phase != CarryPhase.Idle && Phase != CarryPhase.Complete) Enter(Phase);
        }
        public void Cancel() { Phase = CarryPhase.Idle; Grip = 0; }
        public void SetGripOffset(Point2 mouthOffset) { offset = mouthOffset; }

        private void Enter(CarryPhase phase)
        {
            Phase = phase; elapsed = 0;
            petStart = petEnd = PetPosition; iconStart = iconEnd = IconPosition; gripStart = Grip;
            correction = new Point2(iconStart.X - petStart.X - offset.X, iconStart.Y - petStart.Y - offset.Y);
            duration = .6;
            if (phase == CarryPhase.Approach || phase == CarryPhase.Transport)
            {
                Point2 target = phase == CarryPhase.Approach ? move.From : move.To;
                petEnd = DesktopGeometry.Clamp(new Point2(target.X - offset.X, target.Y - offset.Y), bounds, 0);
                // SmoothStep 的最高斜率为 1.5，按此放大时长，斜向移动也不超出90像素/秒。
                double x = petEnd.X - petStart.X, y = petEnd.Y - petStart.Y;
                duration = Math.Max(.1, Math.Sqrt(x * x + y * y) / 60);
            }
            if (phase == CarryPhase.PickUp || phase == CarryPhase.Transport)
                iconEnd = new Point2(petEnd.X + offset.X, petEnd.Y + offset.Y);
            if (phase == CarryPhase.PutDown) iconEnd = move.To;
        }
        private static Point2 Lerp(Point2 a, Point2 b, double t)
        { return new Point2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }
    }
}
