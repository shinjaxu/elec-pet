using System;
using System.Collections.Generic;

namespace ElecPet.Core
{
    public enum DesktopItemCategory { Image, Document, Folder, Shortcut, Other }
    public sealed class DesktopItem
    {
        public readonly string Id, Name, Extension;
        public readonly bool Folder, Shortcut;
        public readonly Point2 Position;
        public DesktopItem(string id, string name, string extension, bool folder, bool shortcut, Point2 position)
        { Id = id; Name = name; Extension = extension; Folder = folder; Shortcut = shortcut; Position = position; }
    }
    public sealed class DesktopSnapshot
    {
        public readonly List<DesktopItem> Items;
        public readonly DesktopRect WorkArea;
        public readonly Point2 GridOrigin, Spacing;
        public readonly bool AutoArrange;
        public readonly string ViewId;
        public readonly int IconSize;
        public DesktopSnapshot(List<DesktopItem> items, DesktopRect workArea, Point2 origin, Point2 spacing, bool autoArrange, string viewId, int iconSize = 32)
        { Items = items; WorkArea = workArea; GridOrigin = origin; Spacing = spacing; AutoArrange = autoArrange; ViewId = viewId; IconSize = iconSize; }
    }
    public sealed class DesktopMove
    {
        public readonly string Id;
        public readonly Point2 From, To;
        public readonly bool Temporary;
        public DesktopMove(string id, Point2 from, Point2 to, bool temporary = false)
        { Id = id; From = from; To = to; Temporary = temporary; }
    }
    public sealed class OrganizationPlan
    {
        public readonly List<DesktopMove> Moves = new List<DesktopMove>();
        public string BlockedReason { get; internal set; }
    }

    public static class DesktopOrganization
    {
        public static Point2 DetectGridOrigin(List<DesktopItem> items, DesktopRect area, Point2 spacing)
        {
            if (spacing.X <= 2 || spacing.Y <= 2) return new Point2(area.X, area.Y);
            var xs = new Dictionary<int, int>(); var ys = new Dictionary<int, int>();
            foreach (var item in items)
            {
                if (!Inside(item.Position, area)) continue;
                int x = (int)Math.Round((item.Position.X - area.X) % spacing.X);
                int y = (int)Math.Round((item.Position.Y - area.Y) % spacing.Y);
                xs[x] = xs.ContainsKey(x) ? xs[x] + 1 : 1;
                ys[y] = ys.ContainsKey(y) ? ys[y] + 1 : 1;
            }
            return new Point2(area.X + MostCommon(xs), area.Y + MostCommon(ys));
        }
        private static int MostCommon(Dictionary<int, int> values)
        {
            int best = 0, frequency = 0;
            foreach (var pair in values)
                if (pair.Value > frequency || (pair.Value == frequency && pair.Key < best))
                { best = pair.Key; frequency = pair.Value; }
            return best;
        }

        public static DesktopItemCategory Classify(DesktopItem item)
        {
            if (item.Shortcut) return DesktopItemCategory.Shortcut;
            if (item.Folder) return DesktopItemCategory.Folder;
            string extension = (item.Extension ?? "").ToLowerInvariant();
            if (Array.IndexOf(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".heic", ".svg", ".tif", ".tiff" }, extension) >= 0)
                return DesktopItemCategory.Image;
            if (Array.IndexOf(new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".csv", ".rtf" }, extension) >= 0)
                return DesktopItemCategory.Document;
            return DesktopItemCategory.Other;
        }

        // 先计算完整布局，再排出腾位顺序；不能先移动部分图标才发现容量不足。
        public static OrganizationPlan Plan(DesktopSnapshot snapshot)
        {
            var plan = new OrganizationPlan();
            if (snapshot.AutoArrange) return Block(plan, "请关闭桌面的自动排列图标，宠物才能搬运。");
            var spacing = snapshot.Spacing;
            if (spacing.X <= 2 || spacing.Y <= 2 || double.IsNaN(spacing.X) || double.IsNaN(spacing.Y))
                return Block(plan, "暂时无法确定桌面图标间距。");
            var sorted = snapshot.Items.FindAll(item => Inside(item.Position, snapshot.WorkArea));
            sorted.Sort((a, b) =>
            {
                int name = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                return name != 0 ? name : StringComparer.Ordinal.Compare(a.Id, b.Id);
            });
            var cells = new List<Point2>();
            var area = snapshot.WorkArea;
            for (double x = snapshot.GridOrigin.X; x + spacing.X <= area.X + area.Width + .01; x += spacing.X)
                for (double y = snapshot.GridOrigin.Y; y + spacing.Y <= area.Y + area.Height + .01; y += spacing.Y)
                    if (x >= area.X && y >= area.Y) cells.Add(new Point2(x, y));
            // 最终目标保留爱心留白，完整网格仍用于下方的循环占位腾挪。
            var layout = HeartLayout(cells, sorted.Count, snapshot);
            if (sorted.Count > layout.Count) return Block(plan, "桌面空间不足，暂时保留原布局。");
            var positions = new Dictionary<string, Point2>(StringComparer.Ordinal);
            var targets = new Dictionary<string, Point2>(StringComparer.Ordinal);
            var pending = new List<string>();
            for (int i = 0; i < sorted.Count; i++)
            {
                var item = sorted[i];
                if (positions.ContainsKey(item.Id)) return Block(plan, "桌面项目身份冲突，等待刷新。");
                positions.Add(item.Id, item.Position); targets.Add(item.Id, layout[i]);
                if (!Near(item.Position, targets[item.Id])) pending.Add(item.Id);
            }
            var parked = new HashSet<string>();
            while (pending.Count > 0)
            {
                int ready = pending.FindIndex(id => Vacant(targets[id], id, positions, spacing));
                if (ready >= 0)
                {
                    string id = pending[ready];
                    plan.Moves.Add(new DesktopMove(id, positions[id], targets[id]));
                    positions[id] = targets[id]; pending.RemoveAt(ready);
                    continue;
                }
                // 闭环只能借一个真实空格解开；不能用重叠图标作为临时堆栈。
                int movable = pending.FindIndex(id => !parked.Contains(id));
                int free = cells.FindIndex(point => Vacant(point, null, positions, spacing));
                if (movable < 0 || free < 0) return Block(plan, "缺少腾挪空间，暂时保留原布局。");
                string moving = pending[movable];
                plan.Moves.Add(new DesktopMove(moving, positions[moving], cells[free], true));
                positions[moving] = cells[free]; parked.Add(moving);
            }
            return plan;
        }

        private static List<Point2> HeartLayout(List<Point2> cells, int count, DesktopSnapshot snapshot)
        {
            if (count < 6 || cells.Count == 0) return cells;
            int rows = cells.FindAll(point => point.X == cells[0].X).Count, columns = cells.Count / rows;
            if (columns < 7 || rows < 6) return cells;
            var spacing = snapshot.Spacing;
            // 按图标可见中心选最近的网格起点，避免实际网格余量让爱心额外偏向一侧。
            double centerX = snapshot.WorkArea.X + (snapshot.WorkArea.Width - snapshot.IconSize) / 2 - cells[0].X;
            double centerY = snapshot.WorkArea.Y + (snapshot.WorkArea.Height - snapshot.IconSize) / 2 - cells[0].Y;
            int left = Math.Max(0, Math.Min(columns - 7, (int)Math.Round(centerX / spacing.X - 3)));
            int top = Math.Max(0, Math.Min(rows - 6, (int)Math.Round(centerY / spacing.Y - 2.5)));
            Point2 origin = cells[left * rows + top];
            int heartCount = Math.Min(12, count);
            var offsets = new List<Point2> { new Point2(3, 5) };
            if (heartCount % 2 == 0) offsets.Add(new Point2(3, 1));
            int pairs = (heartCount - offsets.Count) / 2;
            int[] xs = { 1, 0, 0, 1, 2 }, ys = { 0, 1, 2, 3, 4 };
            // 少量图标仍覆盖对称轮廓，保留底尖与偶数时的中间凹口。
            for (int pair = 0; pair < pairs; pair++)
            {
                int at = (int)Math.Round(pair * 4.0 / (pairs - 1));
                offsets.Add(new Point2(xs[at], ys[at]));
                offsets.Add(new Point2(6 - xs[at], ys[at]));
            }
            offsets.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            var layout = new List<Point2>();
            foreach (var offset in offsets)
                layout.Add(new Point2(origin.X + offset.X * spacing.X, origin.Y + offset.Y * spacing.Y));
            // 溢出项目从左上向下逐列排，整个爱心包围区都留空，避免填满轮廓内部。
            for (int column = 0; column < columns; column++)
                for (int row = 0; row < rows; row++)
                    if (column < left || column >= left + 7 || row < top || row >= top + 6)
                        layout.Add(cells[column * rows + row]);
            return layout;
        }

        public static bool Near(Point2 a, Point2 b)
        { return Math.Abs(a.X - b.X) <= 2 && Math.Abs(a.Y - b.Y) <= 2; }
        public static bool Inside(Point2 p, DesktopRect rect)
        { return p.X >= rect.X && p.Y >= rect.Y && p.X < rect.X + rect.Width && p.Y < rect.Y + rect.Height; }
        private static bool Vacant(Point2 point, string except, Dictionary<string, Point2> positions, Point2 spacing)
        {
            foreach (var pair in positions)
                if (pair.Key != except && Math.Abs(pair.Value.X - point.X) < spacing.X - 2 && Math.Abs(pair.Value.Y - point.Y) < spacing.Y - 2)
                    return false;
            return true;
        }
        private static OrganizationPlan Block(OrganizationPlan plan, string reason)
        { plan.Moves.Clear(); plan.BlockedReason = reason; return plan; }
    }
}
