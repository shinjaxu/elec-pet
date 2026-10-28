using System;
using ElecPet.Core;

namespace ElecPet.Tests
{
    public static class CarryChecks
    {
        private static int count;
        public static int Run()
        {
            count = 0;
            var carry = New();
            carry.Tick(10, 29.99, true);
            Check(carry.Phase == CarryPhase.Approach && carry.PetPosition.X == 200, "空闲不足30秒不启动");
            carry.Tick(.1, 30, true);
            Check(carry.PetPosition.X < 200 && !carry.WantsWrite, "走近时不移动图标");
            var old = carry.PetPosition;
            carry.Tick(100, 30, false);
            Check(carry.PetPosition.X == old.X, "暂停不补算时间");
            Until(carry, CarryPhase.PickUp);
            var source = carry.IconPosition;
            carry.Tick(.3, 30, true);
            Check(carry.Grip > 0 && carry.Grip < 1 && carry.WantsWrite, "抓取阶段图标与叼持渐变");
            Check(Distance(carry.IconPosition, source) < 50, "抓取连续衔接偏移");
            carry.Tick(.3, 30, true);
            Check(carry.Phase == CarryPhase.Transport && carry.Grip == 1, "抓取0.6秒后搬运");
            old = carry.PetPosition;
            carry.Tick(.1, 0, true);
            Check(carry.PetPosition.X == old.X, "输入恢复立即冻结");
            double maxSpeed = 0;
            while (carry.Phase == CarryPhase.Transport)
            {
                old = carry.PetPosition;
                carry.Tick(.01, 30, true);
                maxSpeed = Math.Max(maxSpeed, Distance(carry.PetPosition, old) / .01);
            }
            Check(maxSpeed <= 90.01 && maxSpeed > 80, "斜向搬运速度受限且按距离推进");
            Check(carry.Phase == CarryPhase.PutDown, "先搬到再放下");
            carry.Tick(.3, 30, true);
            Check(carry.Grip > 0 && carry.Grip < 1, "放下平滑松口");
            carry.Tick(.3, 30, true);
            Check(carry.Phase == CarryPhase.Complete && carry.IconPosition.X == 400 && carry.IconPosition.Y == 350, "放下准确命中目标");
            carry = New(); Until(carry, CarryPhase.Transport); carry.Tick(.3, 30, true);
            old = carry.PetPosition;
            var actual = new Point2(142, 150);
            carry.Rebase(old, actual);
            Check(carry.Phase == CarryPhase.Transport && Distance(carry.IconPosition, actual) == 0, "恢复从真实位置继续不重播抓取");
            carry.Tick(0, 30, true);
            Check(Distance(carry.PetPosition, old) == 0, "零时间不移动");
            carry.Cancel(); carry.Tick(10, 30, true);
            Check(carry.Phase == CarryPhase.Idle && !carry.WantsWrite, "取消不再请求写入");
            carry.Begin(new DesktopMove("edge", new Point2(0, 0), new Point2(10, 10)), new Point2(200, 200), new DesktopRect(88, 88, 824, 624), new Point2(0, -28));
            for (int i = 0; i < 1500 && carry.Phase != CarryPhase.Complete; i++)
            {
                carry.Tick(.01, 30, true);
                Check(carry.PetPosition.X >= 88 && carry.PetPosition.Y >= 88, "靠边搬运宠物仍在安全范围");
            }
            Check(carry.Phase == CarryPhase.Complete && carry.IconPosition.X == 10, "边缘图标仍能准确放下");
            carry = New(); Until(carry, CarryPhase.Transport);
            carry.SetGripOffset(new Point2(-55, -10)); carry.Tick(.7, 30, true);
            Check(Math.Abs(carry.IconPosition.X - carry.PetPosition.X + 55) < .01
                && Math.Abs(carry.IconPosition.Y - carry.PetPosition.Y + 10) < .01, "图标跟随实际嘴部投影而不是悬在背上");
            return count;
        }
        private static DesktopCarry New()
        {
            var carry = new DesktopCarry();
            carry.Begin(new DesktopMove("a", new Point2(100, 100), new Point2(400, 350)),
                new Point2(200, 200), new DesktopRect(0, 0, 1000, 1000), new Point2(0, -28));
            return carry;
        }
        private static void Until(DesktopCarry carry, CarryPhase phase)
        {
            for (int i = 0; i < 3000 && carry.Phase != phase; i++) carry.Tick(.01, 30, true);
            if (carry.Phase != phase) throw new InvalidOperationException("搬运阶段未到达：" + phase);
        }
        private static double Distance(Point2 a, Point2 b) { return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)); }
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); count++; }
    }
}
