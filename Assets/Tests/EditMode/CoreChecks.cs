using System;
using ElecPet.Core;

namespace ElecPet.Tests
{
    // 同一组真实逻辑测试既可在 Unity 中执行，也可在未安装 Unity 时执行。
    public static class CoreChecks
    {
        private static int count;

        public static int Run()
        {
            count = 0;
            var bounds = new DesktopRect(0, 0, 1000, 800);
            Equal(DesktopGeometry.ScreenToLocal(new Point2(120, 260), new DesktopRect(20, 60, 1000, 800), 500, 400), 50, 300, "窗口偏移与纵轴转换");
            Equal(DesktopGeometry.Clamp(new Point2(-20, 900), bounds, 50), 50, 750, "拖拽不可越出工作区");
            Equal(DesktopGeometry.OnPerimeter(0, bounds, 50), 50, 50, "底边起点");
            Equal(DesktopGeometry.OnPerimeter(950, bounds, 50), 950, 100, "跨角进入右边");
            Equal(DesktopGeometry.OnPerimeter(1650, bounds, 50), 900, 750, "跨角进入顶边");
            Equal(DesktopGeometry.OnPerimeter(2550, bounds, 50), 50, 700, "跨角进入左边");
            Equal(DesktopGeometry.OnPerimeter(3250, bounds, 50), 100, 50, "绕屏幕一周后继续");
            Equal(DesktopGeometry.OnPerimeter(-50, bounds, 50), 50, 100, "反向距离回绕");
            var small = new DesktopRect(0, 0, 60, 40);
            Equal(DesktopGeometry.Clamp(new Point2(-5, 200), small, 50), 30, 20, "小窗口中角色居中");
            Equal(DesktopGeometry.OnPerimeter(300, small, 50), 30, 20, "小窗口周长为零");
            var simulation = new PetMotion(bounds, 50);
            simulation.Tick(1, false);
            Equal(simulation.Position, 130, 50, "巡游按实际时间移动");
            simulation.Tick(10, true);
            Equal(simulation.Position, 130, 50, "暂停不累计移动");
            simulation.DragTo(new Point2(400, 300));
            Equal(simulation.Position, 400, 300, "拖拽遵守目标位置");
            simulation.Release();
            simulation.Tick(0.5, false);
            Equal(simulation.Position, 400, 260, "松手后平滑返回最近边界");
            simulation.Tick(10, false);
            Equal(simulation.Position, 400, 50, "归边不瞬移也不越界");
            simulation.Resize(new DesktopRect(0, 0, 200, 100));
            Equal(simulation.Position, 150, 50, "分辨率变小重新约束角色");
            return count;
        }

        public static int RunMultiMonitor()
        {
            count = 0;
            // 副屏比主屏高 161 像素；其右上方和左下方都不属于真实桌面。
            var screens = new[] { new DesktopRect(0, 161, 1920, 1032), new DesktopRect(1920, 0, 1920, 1032) };
            var motion = new PetMotion(new DesktopRect(0, 0, 3840, 1193), 88, screens);
            motion.DragTo(new Point2(3000, 1150));
            Equal(motion.Position, 3000, 944, "主屏上方空隙不可进入");
            motion.DragTo(new Point2(300, 40));
            Equal(motion.Position, 300, 249, "副屏下方空隙不可进入");
            motion.DragTo(new Point2(1920, 600));
            Equal(motion.Position, 1920, 600, "两屏共享边缘不形成安全边距隔墙");
            motion.DragTo(new Point2(1900, 600));
            motion.DragTo(new Point2(1940, 600));
            Equal(motion.Position, 1940, 600, "可以连续拖过两屏接缝");
            motion.Release();
            for (int frame = 0; frame < 9000; frame++)
            {
                motion.Tick(1.0 / 30, false);
                var p = motion.Position;
                // 整个 176 像素安全包围盒都必须由真实工作区覆盖。
                bool left = p.X >= 88 && p.X <= 1832 && p.Y >= 249 && p.Y <= 1105;
                bool right = p.X >= 2008 && p.X <= 3752 && p.Y >= 88 && p.Y <= 944;
                bool crossing = p.X >= 1832 && p.X <= 2008 && p.Y >= 249 && p.Y <= 944;
                if (!left && !right && !crossing) throw new InvalidOperationException("巡游或归边越过真实屏幕：" + p.X + "," + p.Y);
                count++;
            }
            motion.Resize(new DesktopRect(1920, 0, 1920, 1032), new[] { screens[1] });
            motion.DragTo(new Point2(300, 600));
            Equal(motion.Position, 2008, 600, "断开副屏后不能进入旧屏幕");
            var vertical = new[] { new DesktopRect(0, 0, 1032, 1920), new DesktopRect(161, 1920, 1032, 1920) };
            motion = new PetMotion(new DesktopRect(0, 0, 1193, 3840), 88, vertical);
            motion.DragTo(new Point2(40, 3000));
            Equal(motion.Position, 249, 3000, "上下错位屏幕左侧空隙不可进入");
            motion.DragTo(new Point2(1150, 300));
            Equal(motion.Position, 944, 300, "上下错位屏幕右侧空隙不可进入");
            motion.DragTo(new Point2(600, 1920));
            Equal(motion.Position, 600, 1920, "上下屏幕共享边缘允许通过");
            motion = new PetMotion(new DesktopRect(0, 0, 2300, 800), 88,
                new[] { new DesktopRect(0, 0, 1000, 800), new DesktopRect(1300, 0, 1000, 800) });
            motion.DragTo(new Point2(1150, 400));
            Equal(motion.Position, 912, 400, "物理间隔不能被误当作跨屏通道");
            return count;
        }

        private static void Equal(Point2 actual, double x, double y, string label)
        {
            if (Math.Abs(actual.X - x) > 0.001 || Math.Abs(actual.Y - y) > 0.001)
                throw new InvalidOperationException(label + ": expected (" + x + ", " + y + "), got (" + actual.X + ", " + actual.Y + ")");
            count++;
        }
    }
}
