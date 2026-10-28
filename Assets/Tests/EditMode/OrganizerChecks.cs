using System;
using System.Collections.Generic;
using System.Threading;
using ElecPet.Core;
using ElecPet.Runtime;
using ElecPet.Windows;

namespace ElecPet.Tests
{
    public static class OrganizerChecks
    {
        public static int Run()
        {
            int count = 0;
            var area = new DesktopRect(0, 0, 1000, 700);
            var session = new DesktopOrganizationPreview(area);
            using (var worker = new DesktopIconWorker(() => session, () => 30))
            using (var organizer = new PetDesktopOrganizer(worker, area))
            {
                Point2 pet = new Point2(400, 400);
                for (int i = 0; i < 30; i++) { organizer.Tick(.1, 29.99, true, pet); Thread.Sleep(2); }
                Check(!organizer.Active && organizer.Latest != null, "有散乱项目但未空闲不启动", ref count);
                for (int i = 0; i < 100 && !organizer.Active; i++) { organizer.Tick(.1, 30, true, pet); Thread.Sleep(2); }
                Check(organizer.Active, "空闲30秒自动生成任务", ref count);
                for (int i = 0; i < 100; i++) { organizer.Tick(.1, 30, true, pet); pet = organizer.PetPosition; Thread.Sleep(2); }
                var before = organizer.PetPosition;
                organizer.Tick(50, 0, true, pet);
                Check(organizer.Active && organizer.PetPosition.X == before.X, "用户输入保持任务并冻结位置", ref count);
                for (int i = 0; i < 100; i++) { organizer.Tick(.1, 30, true, pet); pet = organizer.PetPosition; Thread.Sleep(2); }
                organizer.Interrupt();
                Check(!organizer.Active, "提起宠物取消整理", ref count);
                for (int i = 0; i < 4000; i++)
                {
                    organizer.Tick(.1, 30, true, pet);
                    if (organizer.Active) pet = organizer.PetPosition;
                    Thread.Sleep(1);
                    if (!organizer.Active && organizer.Status == "桌面整理完成") break;
                }
                Check(!organizer.Active && organizer.Status == "桌面整理完成", "所有图标读回确认后完成", ref count);
                Check(organizer.CompletedBatches == 1 && organizer.CompletedMoves > 0, "成功汇总只在整批读回确认后产生", ref count);
                Check(DesktopOrganization.Plan(organizer.Latest).Moves.Count == 0, "最终布局无需重复整理", ref count);
                for (int i = 0; i < 200; i++) { organizer.Tick(.1, 30, true, pet); Thread.Sleep(1); }
                Check(!organizer.Active, "无变化桌面持续保持完成", ref count);
            }
            CheckSparseDesktopResume(ref count);
            CheckNewTargetOccupant(ref count);
            return count;
        }

        private static void CheckSparseDesktopResume(ref int count)
        {
            var area = new DesktopRect(0, 0, 1000, 700);
            var desktop = new LiveDesktop(area);
            using (var worker = new DesktopIconWorker(() => desktop, () => 30))
            using (var organizer = new PetDesktopOrganizer(worker, area))
            {
                var pet = new Point2(500, 400);
                for (int i = 0; i < 1000 && organizer.Phase != CarryPhase.Transport; i++)
                { organizer.Tick(.1, 30, true, pet); if (organizer.Active) pet = organizer.PetPosition; Thread.Sleep(1); }
                Check(organizer.Phase == CarryPhase.Transport, "稀疏桌面开始搬运", ref count);
                var origin = organizer.Latest.GridOrigin;
                var target = organizer.LookTarget;
                for (int i = 0; i < 15; i++) { organizer.Tick(.1, 30, true, pet); pet = organizer.PetPosition; Thread.Sleep(1); }
                organizer.Tick(.1, 0, true, pet);
                var before = organizer.Latest;
                for (int i = 0; i < 100 && ReferenceEquals(before, organizer.Latest); i++)
                { organizer.Tick(.1, 30, true, pet); Thread.Sleep(2); }
                Check(organizer.Latest.GridOrigin.X == origin.X && organizer.Latest.GridOrigin.Y == origin.Y,
                    "单图标搬运中的非网格位置不重定义网格原点", ref count);
                Check(organizer.Active && organizer.Phase == CarryPhase.Transport && organizer.LookTarget.X == target.X
                    && organizer.LookTarget.Y == target.Y, "暂停后保留原目标并继续搬运", ref count);
                organizer.Tick(.1, 0, true, pet);
                desktop.Relocate(new Point2(510, 220));
                for (int i = 0; i < 100 && organizer.Latest.Items[0].Position.X != 510; i++)
                { organizer.Tick(.1, 30, true, pet); Thread.Sleep(2); }
                Check(organizer.Latest.Items[0].Position.X == 510 && organizer.Phase != CarryPhase.Transport,
                    "暂停时用户调整位置后重建任务而不是恢复旧写入", ref count);
                organizer.Resize(new DesktopRect(0, 0, 800, 600));
                Check(!organizer.Active && organizer.Latest == null, "工作区变化使旧计划失效", ref count);
            }
        }

        private sealed class LiveDesktop : IDesktopIconSession
        {
            private readonly object gate = new object();
            private readonly DesktopRect area;
            private Point2 point = new Point2(350, 230);
            private DesktopItem obstacle;
            public LiveDesktop(DesktopRect area) { this.area = area; }
            public void Relocate(Point2 position) { lock (gate) point = position; }
            public void AddObstacle(Point2 position)
            { lock (gate) obstacle = new DesktopItem("new", "aaa.png", ".png", false, false, position); }
            public DesktopSnapshot Read()
            {
                lock (gate)
                {
                    var items = new List<DesktopItem> { new DesktopItem("one", "one.png", ".png", false, false, point) };
                    if (obstacle != null) items.Add(obstacle);
                    var spacing = new Point2(92, 86);
                    return new DesktopSnapshot(items, area, DesktopOrganization.DetectGridOrigin(items, area, spacing), spacing, false, "live");
                }
            }
            public Point2 Position(string id) { lock (gate) return point; }
            public Point2 Move(string id, Point2 value) { lock (gate) { point = value; return point; } }
            public void RestoreAlignment() { }
            public void Dispose() { }
        }
        private static void CheckNewTargetOccupant(ref int count)
        {
            var area = new DesktopRect(0, 0, 1000, 700);
            var desktop = new LiveDesktop(area);
            using (var worker = new DesktopIconWorker(() => desktop, () => 30))
            using (var organizer = new PetDesktopOrganizer(worker, area))
            {
                var pet = new Point2(500, 400);
                for (int i = 0; i < 1000 && organizer.Phase != CarryPhase.Transport; i++)
                { organizer.Tick(.1, 30, true, pet); if (organizer.Active) pet = organizer.PetPosition; Thread.Sleep(1); }
                var target = organizer.LookTarget;
                desktop.AddObstacle(target);
                for (int i = 0; i < 1500 && organizer.Latest.Items.Count != 2; i++)
                { organizer.Tick(.1, 30, true, pet); if (organizer.Active) pet = organizer.PetPosition; Thread.Sleep(1); }
                Check(organizer.Latest.Items.Count == 2 && !DesktopOrganization.Near(desktop.Position("one"), target),
                    "后台新增图标占用目标时放下前核对并重新规划，不能叠放", ref count);
            }
        }
        private static void Check(bool ok, string label, ref int count)
        { if (!ok) throw new InvalidOperationException(label); count++; }
    }
}
