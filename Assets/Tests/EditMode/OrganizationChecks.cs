using System;
using System.Collections.Generic;
using ElecPet.Core;

namespace ElecPet.Tests
{
    public static class OrganizationChecks
    {
        private static int count;
        private static DesktopItem Item(string id, double x, double y, string extension = ".png", bool folder = false, bool link = false)
        { return new DesktopItem(id, "同名", extension, folder, link, new Point2(x, y)); }
        private static DesktopSnapshot Snapshot(params DesktopItem[] items)
        { return new DesktopSnapshot(new List<DesktopItem>(items), new DesktopRect(0, 0, 300, 300), new Point2(0, 0), new Point2(100, 100), false, "test"); }
        public static int Run()
        {
            count = 0;
            Check(DesktopOrganization.Classify(Item("p", 0, 0, ".PNG")) == DesktopItemCategory.Image, "图片扩展名忽略大小写");
            Check(DesktopOrganization.Classify(Item("p", 0, 0, ".lnk", true, true)) == DesktopItemCategory.Shortcut, "目录快捷方式优先识别为链接");
            Check(DesktopOrganization.Classify(Item("p", 0, 0, ".pdf")) == DesktopItemCategory.Document, "文档分类");
            Check(DesktopOrganization.Classify(Item("p", 0, 0, "", true)) == DesktopItemCategory.Folder, "文件夹分类");
            Check(DesktopOrganization.Classify(Item("p", 0, 0, ".exe")) == DesktopItemCategory.Other, "其他分类");
            Check(DesktopOrganization.Plan(Snapshot()).Moves.Count == 0, "空桌面不产生任务");
            Check(DesktopOrganization.Plan(Snapshot(Item("a", 1, 2))).Moves.Count == 0, "舍入误差不反复整理");
            var forward = DesktopOrganization.Plan(Snapshot(Item("b", 240, 240), Item("a", 240, 100)));
            var reverse = DesktopOrganization.Plan(Snapshot(Item("a", 240, 100), Item("b", 240, 240)));
            Check(forward.Moves.Count == 2 && reverse.Moves.Count == 2, "同名项目身份不同");
            Check(forward.Moves[0].Id == "a" && forward.Moves[0].To.X == 0 && forward.Moves[0].To.Y == 0, "枚举顺序不影响目标");
            Check(reverse.Moves[0].Id == "a" && reverse.Moves[1].To.Y == 100, "同名项目按身份从上到下稳定排列");
            var full = new List<DesktopItem>();
            for (int i = 0; i < 9; i++) full.Add(Item("i" + i, i / 3 * 100, i % 3 * 100));
            Check(DesktopOrganization.Plan(Snapshot(full.ToArray())).BlockedReason == null, "九个完整单元恰好容纳");
            full.Add(Item("extra", 280, 280));
            var overflow = DesktopOrganization.Plan(Snapshot(full.ToArray()));
            Check(overflow.BlockedReason != null && overflow.Moves.Count == 0, "容量不足整批不执行");
            var cycle = DesktopOrganization.Plan(Snapshot(Item("a", 0, 100), Item("b", 0, 0)));
            Check(cycle.Moves.Count == 3 && cycle.Moves[0].Temporary, "互换先搬到临时空位");
            Check(cycle.Moves[0].To.X == 0 && cycle.Moves[0].To.Y == 200, "使用首个空槽");
            Check(cycle.Moves[1].Id == "b" && cycle.Moves[2].Id == "a", "腾位后再完成互换");
            full.RemoveAt(9);
            full[0] = Item("i0", 0, 100); full[1] = Item("i1", 0, 0);
            var noSpace = DesktopOrganization.Plan(Snapshot(full.ToArray()));
            Check(noSpace.BlockedReason != null && noSpace.Moves.Count == 0, "满屏互换不叠放");
            var multi = DesktopOrganization.Plan(Snapshot(Item("a", 0, 0), Item("offscreen", -100, 100)));
            Check(multi.Moves.Count == 0 && multi.BlockedReason == null, "副屏图标不参与整理");
            var bad = Snapshot();
            bad = new DesktopSnapshot(bad.Items, bad.WorkArea, bad.GridOrigin, new Point2(0, 100), false, "test");
            Check(DesktopOrganization.Plan(bad).BlockedReason != null, "无效间距停止规划");
            var auto = new DesktopSnapshot(new List<DesktopItem> { Item("a", 100, 100) }, new DesktopRect(0, 0, 300, 300), new Point2(0, 0), new Point2(100, 100), true, "test");
            Check(DesktopOrganization.Plan(auto).Moves.Count == 0 && DesktopOrganization.Plan(auto).BlockedReason != null, "自动排列不写入");
            var increment = DesktopOrganization.Plan(Snapshot(Item("a", 0, 0), Item("b", 240, 240)));
            Check(increment.Moves.Count == 1 && increment.Moves[0].Id == "b", "只处理目标变化项目");
            var grid = DesktopOrganization.DetectGridOrigin(new List<DesktopItem> { Item("a", 22, 2), Item("b", 98, 86), Item("moving", 161, 143) },
                new DesktopRect(0, 0, 1920, 1080), new Point2(76, 84));
            Check(grid.X == 22 && grid.Y == 2, "网格原点采用实测多数偏移，不随单个搬运项目漂移");
            HeartOutlineIsCenteredAndIdempotent();
            OffsetGridCentersHeartWithinHalfCell();
            OverflowPreservesNameOrderAndHeartInterior();
            SparseHeartsRemainSymmetric();
            HeartCyclesUseRealVacantCells();
            HeartCapacityFailsBeforeMoving();
            SmallLayoutsKeepSafeColumnOrder();
            return count;
        }

        public static void HeartOutlineIsCenteredAndIdempotent()
        {
            var source = HeartSnapshot(12);
            var result = ApplyPlan(source);
            var expected = HeartPoints(source);
            foreach (var point in expected)
                Check(result.Items.Exists(item => DesktopOrganization.Near(item.Position, point)), "十二个图标必须组成居中的完整空心爱心");
            Check(DesktopOrganization.Plan(result).Moves.Count == 0, "爱心整理完成后不反复搬运");
            var reversed = new List<DesktopItem>(source.Items); reversed.Reverse();
            var reordered = ApplyPlan(Copy(source, reversed));
            foreach (var item in result.Items)
                Check(DesktopOrganization.Near(item.Position, reordered.Items.Find(value => value.Id == item.Id).Position),
                    "枚举顺序改变不能改变爱心目标");
        }

        public static void OverflowPreservesNameOrderAndHeartInterior()
        {
            var source = HeartSnapshot(16);
            var result = ApplyPlan(source);
            for (int i = 0; i < 12; i++)
                Check(HeartPoints(source).Exists(point => DesktopOrganization.Near(point, result.Items[i].Position)),
                    "名称靠前的十二项进入爱心，不按类型优先占位");
            for (int i = 12; i < 16; i++)
                Check(DesktopOrganization.Near(result.Items[i].Position,
                    new Point2(source.GridOrigin.X, source.GridOrigin.Y + (i - 12) * source.Spacing.Y)),
                    "多余图标按名称顺序从左上向下排放");
            var left = HeartOrigin(source);
            foreach (var item in result.Items)
                if (item.Position.X >= left.X && item.Position.X < left.X + 7 * source.Spacing.X
                    && item.Position.Y >= left.Y && item.Position.Y < left.Y + 6 * source.Spacing.Y)
                    Check(HeartPoints(source).Exists(point => DesktopOrganization.Near(point, item.Position)),
                        "爱心包围区域内部与双瓣凹口不能填入溢出图标");
            Check(DesktopOrganization.Plan(result).Moves.Count == 0, "爱心加溢出布局再次规划应无变化");
        }

        public static void OffsetGridCentersHeartWithinHalfCell()
        {
            var items = new List<DesktopItem>();
            for (int i = 0; i < 12; i++)
                items.Add(new DesktopItem("offset-" + i, i.ToString("D3"), ".png", false, false,
                    new Point2(22, 2 + i * 84)));
            var source = new DesktopSnapshot(items, new DesktopRect(0, 0, 1920, 1040), new Point2(22, 2),
                new Point2(76, 84), false, "offset-heart", 32);
            var result = ApplyPlan(source);
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var item in result.Items)
            {
                minX = Math.Min(minX, item.Position.X); maxX = Math.Max(maxX, item.Position.X);
                minY = Math.Min(minY, item.Position.Y); maxY = Math.Max(maxY, item.Position.Y);
            }
            // 实测图标坐标带有网格偏移；居中以图标可见包围盒为准，不能只取可用列数的一半。
            double centerX = (minX + maxX + source.IconSize) / 2;
            double centerY = (minY + maxY + source.IconSize) / 2;
            Check(Math.Abs(centerX - (source.WorkArea.X + source.WorkArea.Width / 2)) <= source.Spacing.X / 2,
                "真实偏移网格的爱心可见中心与工作区水平中心距离不能超过半格");
            Check(Math.Abs(centerY - (source.WorkArea.Y + source.WorkArea.Height / 2)) <= source.Spacing.Y / 2,
                "真实偏移网格的爱心可见中心与工作区垂直中心距离不能超过半格");
        }

        public static void SparseHeartsRemainSymmetric()
        {
            for (int amount = 6; amount <= 11; amount++)
            {
                var source = HeartSnapshot(amount);
                var result = ApplyPlan(source);
                var origin = HeartOrigin(source);
                double axis = origin.X + 3 * source.Spacing.X;
                foreach (var item in result.Items)
                {
                    Check(HeartPoints(source).Exists(point => DesktopOrganization.Near(point, item.Position)),
                        "稀疏爱心只使用已批准的轮廓网格");
                    var mirror = new Point2(2 * axis - item.Position.X, item.Position.Y);
                    Check(result.Items.Exists(value => DesktopOrganization.Near(value.Position, mirror)),
                        "六到十一个图标的稀疏爱心保持左右对称");
                }
                Check(result.Items.Exists(item => DesktopOrganization.Near(item.Position,
                    new Point2(axis, origin.Y + 5 * source.Spacing.Y))), "稀疏爱心保留底尖");
                Check(DesktopOrganization.Plan(result).Moves.Count == 0, "稀疏爱心不会反复调整");
            }
        }

        public static void HeartCyclesUseRealVacantCells()
        {
            var arranged = ApplyPlan(HeartSnapshot(12));
            var items = new List<DesktopItem>(arranged.Items);
            var first = items[0]; var second = items[1];
            items[0] = At(first, second.Position); items[1] = At(second, first.Position);
            var swapped = Copy(arranged, items);
            var plan = DesktopOrganization.Plan(swapped);
            Check(plan.BlockedReason == null && plan.Moves.Count == 3 && plan.Moves[0].Temporary,
                "爱心中的两项交换借用真实空槽解环");
            ApplyPlan(swapped);
        }

        public static void HeartCapacityFailsBeforeMoving()
        {
            // 十三列十行共130格，爱心包围区域42格只放12项，因此最多容纳100项。
            var source = HeartSnapshot(101);
            var plan = DesktopOrganization.Plan(source);
            Check(plan.BlockedReason != null && plan.Moves.Count == 0,
                "不能为容纳溢出图标侵占空心爱心内部，空间不足整批不执行");
            var exact = ApplyPlan(HeartSnapshot(100));
            Check(DesktopOrganization.Plan(exact).Moves.Count == 0, "爱心及盒外空间恰好放满后仍保持稳定");
        }

        public static void SmallLayoutsKeepSafeColumnOrder()
        {
            for (int amount = 1; amount < 6; amount++)
            {
                var result = ApplyPlan(HeartSnapshot(amount));
                for (int i = 0; i < amount; i++)
                    Check(DesktopOrganization.Near(result.Items[i].Position,
                        new Point2(result.GridOrigin.X, result.GridOrigin.Y + i * result.Spacing.Y)),
                        "少于六项回退左上规整，不制造重复目标");
            }
            var small = Snapshot(Item("b", 240, 240, ".png"), Item("a", 240, 100, ".txt"));
            var ordered = ApplyPlan(small);
            Check(ordered.Items.Find(item => item.Id == "a").Position.Y == 0,
                "小屏回退同样按名称与身份排序，不按类别抢先");
            var source = HeartSnapshot(12);
            source.Items.Add(new DesktopItem("offscreen", "000-before.png", ".png", false, false, new Point2(-500, 100)));
            var resultWithSecondScreen = ApplyPlan(source);
            Check(DesktopOrganization.Near(resultWithSecondScreen.Items.Find(item => item.Id == "offscreen").Position,
                new Point2(-500, 100)), "爱心规划仍不移动副屏图标");
        }

        private static DesktopSnapshot HeartSnapshot(int amount)
        {
            var items = new List<DesktopItem>();
            for (int i = 0; i < amount; i++)
                items.Add(new DesktopItem("item-" + i.ToString("D3"), i.ToString("D3"), i % 3 == 0 ? ".png" : ".txt",
                    false, false, new Point2(20 + (12 - i / 10) * 100, 10 + i % 10 * 100)));
            return new DesktopSnapshot(items, new DesktopRect(20, 10, 1300, 1000), new Point2(20, 10),
                new Point2(100, 100), false, "heart-test");
        }

        private static Point2 HeartOrigin(DesktopSnapshot source)
        { return new Point2(source.GridOrigin.X + 3 * source.Spacing.X, source.GridOrigin.Y + 2 * source.Spacing.Y); }

        private static List<Point2> HeartPoints(DesktopSnapshot source)
        {
            var result = new List<Point2>();
            var origin = HeartOrigin(source);
            int[,] points = { { 1, 0 }, { 5, 0 }, { 0, 1 }, { 3, 1 }, { 6, 1 }, { 0, 2 }, { 6, 2 },
                { 1, 3 }, { 5, 3 }, { 2, 4 }, { 4, 4 }, { 3, 5 } };
            for (int i = 0; i < points.GetLength(0); i++)
                result.Add(new Point2(origin.X + points[i, 0] * source.Spacing.X, origin.Y + points[i, 1] * source.Spacing.Y));
            return result;
        }

        private static DesktopSnapshot ApplyPlan(DesktopSnapshot source)
        {
            var plan = DesktopOrganization.Plan(source);
            Check(plan.BlockedReason == null, "合成桌面有足够安全空间，应能完成规划");
            var positions = new Dictionary<string, Point2>();
            foreach (var item in source.Items) positions.Add(item.Id, item.Position);
            foreach (var move in plan.Moves)
            {
                Check(DesktopOrganization.Near(positions[move.Id], move.From), "每次搬运从当前已知位置开始");
                Check(move.To.X >= source.WorkArea.X && move.To.Y >= source.WorkArea.Y
                    && move.To.X + source.Spacing.X <= source.WorkArea.X + source.WorkArea.Width + .01
                    && move.To.Y + source.Spacing.Y <= source.WorkArea.Y + source.WorkArea.Height + .01,
                    "最终与临时落点都保留完整网格安全边界");
                foreach (var other in positions)
                    if (other.Key != move.Id)
                        Check(Math.Abs(other.Value.X - move.To.X) >= source.Spacing.X - 2
                            || Math.Abs(other.Value.Y - move.To.Y) >= source.Spacing.Y - 2,
                            "每一步搬运都不能叠放其他图标");
                positions[move.Id] = move.To;
            }
            var items = new List<DesktopItem>();
            foreach (var item in source.Items) items.Add(At(item, positions[item.Id]));
            return Copy(source, items);
        }

        private static DesktopItem At(DesktopItem item, Point2 point)
        { return new DesktopItem(item.Id, item.Name, item.Extension, item.Folder, item.Shortcut, point); }

        private static DesktopSnapshot Copy(DesktopSnapshot source, List<DesktopItem> items)
        { return new DesktopSnapshot(items, source.WorkArea, source.GridOrigin, source.Spacing, source.AutoArrange, source.ViewId, source.IconSize); }
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); count++; }
    }
}
