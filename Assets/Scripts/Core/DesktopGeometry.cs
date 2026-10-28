using System;
using System.Collections.Generic;

namespace ElecPet.Core
{
    public struct Point2
    {
        public readonly double X;
        public readonly double Y;
        public Point2(double x, double y) { X = x; Y = y; }
    }

    public struct DesktopRect
    {
        public readonly double X, Y, Width, Height;
        public DesktopRect(double x, double y, double width, double height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
    }

    public static class DesktopGeometry
    {
        // Win32 左上原点转换为 Unity 左下原点，独立处理横纵缩放。
        public static Point2 ScreenToLocal(Point2 point, DesktopRect window, double width, double height)
        {
            return new Point2((point.X - window.X) * width / window.Width,
                height - (point.Y - window.Y) * height / window.Height);
        }

        public static Point2 Clamp(Point2 point, DesktopRect bounds, double margin)
        {
            double mx = Math.Min(margin, bounds.Width / 2), my = Math.Min(margin, bounds.Height / 2);
            return new Point2(Math.Max(bounds.X + mx, Math.Min(bounds.X + bounds.Width - mx, point.X)),
                Math.Max(bounds.Y + my, Math.Min(bounds.Y + bounds.Height - my, point.Y)));
        }

        // 各屏外沿保留宠物体积；相接屏幕的重叠区补通道，避免接缝被两侧边距阻断。
        public static DesktopRect[] SafeMovementAreas(DesktopRect[] workAreas, double margin)
        {
            var safe = new List<DesktopRect>();
            foreach (var work in workAreas)
            {
                var low = Clamp(new Point2(work.X, work.Y), work, margin);
                var high = Clamp(new Point2(work.X + work.Width, work.Y + work.Height), work, margin);
                safe.Add(new DesktopRect(low.X, low.Y, high.X - low.X, high.Y - low.Y));
            }
            for (int i = 0; i < workAreas.Length; i++)
                for (int j = i + 1; j < workAreas.Length; j++)
                {
                    var a = workAreas[i]; var b = workAreas[j];
                    var sa = safe[i]; var sb = safe[j];
                    double left = Math.Max(sa.X, sb.X), right = Math.Min(sa.X + sa.Width, sb.X + sb.Width);
                    double bottom = Math.Max(sa.Y, sb.Y), top = Math.Min(sa.Y + sa.Height, sb.Y + sb.Height);
                    if ((Math.Abs(a.X + a.Width - b.X) < .001 || Math.Abs(b.X + b.Width - a.X) < .001) && top >= bottom)
                    {
                        double x = Math.Min(sa.X, sb.X);
                        safe.Add(new DesktopRect(x, bottom, Math.Max(sa.X + sa.Width, sb.X + sb.Width) - x, top - bottom));
                    }
                    if ((Math.Abs(a.Y + a.Height - b.Y) < .001 || Math.Abs(b.Y + b.Height - a.Y) < .001) && right >= left)
                    {
                        double y = Math.Min(sa.Y, sb.Y);
                        safe.Add(new DesktopRect(left, y, right - left, Math.Max(sa.Y + sa.Height, sb.Y + sb.Height) - y));
                    }
                }
            return safe.ToArray();
        }

        public static Point2 ClampToAreas(Point2 point, DesktopRect[] areas)
        {
            Point2 nearest = point;
            double best = double.MaxValue;
            foreach (var area in areas)
            {
                var candidate = Clamp(point, area, 0);
                double dx = point.X - candidate.X, dy = point.Y - candidate.Y;
                double distance = dx * dx + dy * dy;
                if (distance < best) { best = distance; nearest = candidate; }
            }
            return nearest;
        }

        public static Point2 OnPerimeter(double distance, DesktopRect bounds, double margin)
        {
            var low = Clamp(new Point2(bounds.X, bounds.Y), bounds, margin);
            var high = Clamp(new Point2(bounds.X + bounds.Width, bounds.Y + bounds.Height), bounds, margin);
            double width = high.X - low.X, height = high.Y - low.Y;
            double perimeter = 2 * (width + height);
            if (perimeter <= 0) return low;
            double d = (distance % perimeter + perimeter) % perimeter;
            if (d <= width) return new Point2(low.X + d, low.Y);
            d -= width;
            if (d <= height) return new Point2(high.X, low.Y + d);
            d -= height;
            if (d <= width) return new Point2(high.X - d, high.Y);
            return new Point2(low.X, high.Y - (d - width));
        }

        public static double NearestPerimeterDistance(Point2 point, DesktopRect bounds, double margin)
        {
            var low = Clamp(new Point2(bounds.X, bounds.Y), bounds, margin);
            var high = Clamp(new Point2(bounds.X + bounds.Width, bounds.Y + bounds.Height), bounds, margin);
            var p = Clamp(point, bounds, margin);
            double width = high.X - low.X, height = high.Y - low.Y;
            double best = p.Y - low.Y, distance = p.X - low.X;
            if (high.X - p.X < best) { best = high.X - p.X; distance = width + p.Y - low.Y; }
            if (high.Y - p.Y < best) { best = high.Y - p.Y; distance = width + height + high.X - p.X; }
            if (p.X - low.X < best) distance = 2 * width + height + high.Y - p.Y;
            return distance;
        }

        public static Point2 MoveTowards(Point2 start, Point2 target, double step)
        {
            double x = target.X - start.X, y = target.Y - start.Y;
            double length = Math.Sqrt(x * x + y * y);
            if (length <= step || length == 0) return target;
            return new Point2(start.X + x * step / length, start.Y + y * step / length);
        }
    }
}
