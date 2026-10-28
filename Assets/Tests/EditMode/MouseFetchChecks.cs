using System;
using ElecPet.Core;
namespace ElecPet.Tests
{
    public static class MouseFetchChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static int Run()
        {
            var fetch = new PetMouseFetch();
            var pet = new Point2(300, 300);
            fetch.Tick(.03, pet, new Point2(420, 300), true);
            Check(fetch.Phase == MouseFetchPhase.Stalking, "靠近应伏低");
            fetch.Tick(.1, pet, new Point2(430, 300), true);
            Check(!fetch.BallVisible, "慢移不能抛球");
            fetch.Tick(.1, pet, new Point2(650, 300), true);
            fetch.Tick(.1, pet, new Point2(650, 300), true);
            fetch.Tick(.1, pet, new Point2(650, 300), true);
            Check(fetch.Phase == MouseFetchPhase.Fetching && fetch.Ball.X == 650, "甩动停稳后在终点形成球");
            fetch.Tick(.1, fetch.Ball, new Point2(800, 300), true);
            Check(fetch.Phase == MouseFetchPhase.PickingUp, "到球附近低头拾取");
            fetch.Tick(.4, fetch.Ball, new Point2(800, 300), true);
            Check(fetch.Carrying, "拾取完成叼球");
            fetch.Tick(.1, new Point2(650, 300), new Point2(900, 300), true);
            Check(fetch.Target.X == 900, "叼球持续追随鼠标");
            fetch.Tick(.1, new Point2(840, 300), new Point2(900, 300), true);
            Check(fetch.Phase == MouseFetchPhase.Spiraling, "接近后起飞");
            var landing = fetch.Landing;
            fetch.Tick(1.2, new Point2(840, 300), new Point2(1100, 600), true);
            Check(fetch.Landing.X == landing.X && fetch.Landing.Y == landing.Y, "起飞后落点固定");
            Check(Math.Abs(fetch.FlightAngle - Math.PI * 2) < .001, "半程转一圈");
            fetch.Tick(1.2, landing, new Point2(1100, 600), true);
            Check(fetch.Phase == MouseFetchPhase.Dropping && Math.Abs(fetch.FlightAngle - Math.PI * 4) < .001, "两圈后落地");
            fetch.Tick(.6, landing, landing, true);
            Check(!fetch.Carrying && fetch.BallVisible, "放下后球留在地面");
            fetch.Tick(.5, landing, landing, true);
            Check(!fetch.Active && !fetch.BallVisible, "放球后球消失");
            fetch.Tick(.1, pet, new Point2(400, 300), false);
            Check(!fetch.Active, "不可用时取消");
            var edge = new PetMouseFetch();
            edge.Tick(.1, pet, new Point2(420, 300), true);
            edge.Tick(.1, pet, new Point2(650, 300), true);
            edge.Tick(.1, pet, new Point2(650, 300), false);
            Check(!edge.BallVisible && !edge.Active, "取消未完成甩动");
            var far = new PetMouseFetch();
            far.Tick(.1, pet, new Point2(1000, 300), true);
            Check(!far.Active, "远处鼠标不唤起");
            var stuck = new PetMouseFetch();
            stuck.Tick(.1, pet, new Point2(420, 300), true);
            stuck.Tick(.1, pet, new Point2(900, 300), true);
            stuck.Tick(.1, pet, new Point2(900, 300), true);
            stuck.Tick(.1, pet, new Point2(900, 300), true);
            for (int i = 0; i < 20; i++) stuck.Tick(.1, pet, new Point2(900, 300), true);
            Check(!stuck.Active && !stuck.BallVisible, "无法前进时清理球并释放自主行为");
            var screens = new[] { new DesktopRect(0, 0, 500, 800), new DesktopRect(800, 0, 500, 800) };
            var motion = new PetMotion(new DesktopRect(0, 0, 1300, 800), 88, screens);
            motion.DragTo(pet);
            var separated = new PetMouseFetch();
            separated.Tick(.1, motion.Position, new Point2(420, 300), true);
            for (int i = 0; i < 50; i++)
            {
                separated.Tick(.1, motion.Position, new Point2(900, 300), true);
                separated.ConstrainTargets(motion.SafeTarget);
                if (separated.Phase == MouseFetchPhase.Fetching)
                    motion.DragTo(DesktopGeometry.MoveTowards(motion.Position, separated.Target, 42));
            }
            Check(!separated.Active && !separated.BallVisible, "不连通双屏不会永久卡住捡球");
            return 16;
        }
    }
}
