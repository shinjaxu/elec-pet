using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ElecPet.Core;

namespace ElecPet.Windows
{
    public enum PetCommand { None, Pause, Hide, Quit, ToggleScreenCompanion }

    public sealed class NativeWindow : IDisposable
    {
        private const int ExStyle = -20, Style = -16, WndProcIndex = -4;
        private const int Transparent = 0x20, Layered = 0x80000, NoActivate = 0x08000000, ToolWindow = 0x80;
        private readonly IntPtr hwnd;
        private readonly int originalStyle, originalExStyle;
        private readonly WindowProc callback;
        private IntPtr previousProc;
        private bool disposed;
        private WindowsKeyboardIdle keyboardIdle;
        private PetCommand pending;
        private Point2? leftPress, leftRelease;
        public DesktopRect Bounds { get; private set; }
        public DesktopRect WorkArea { get; private set; }
        public DesktopRect[] WorkAreas { get; private set; }
        public IntPtr Handle { get { return hwnd; } }
        public float DpiScale { get { uint dpi = GetDpiForWindow(hwnd); return dpi == 0 ? 1 : Math.Max(1, dpi / 96f); } }
        public double KeyboardIdleSeconds { get { return keyboardIdle.Seconds; } }

        public NativeWindow()
        {
            // 仅接受当前进程的主窗口，绝不能把前台其他程序的窗口改成透明。
            using (var process = Process.GetCurrentProcess()) hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Unity 主窗口尚未就绪。");
            originalStyle = GetWindowLong(hwnd, Style);
            originalExStyle = GetWindowLong(hwnd, ExStyle);
            callback = HandleMessage;
            try
            {
                previousProc = SetWindowLongPtr(hwnd, WndProcIndex, Marshal.GetFunctionPointerForDelegate(callback));
                if (previousProc == IntPtr.Zero) throw new Win32Exception();
                SetWindowLong(hwnd, Style, (originalStyle & ~0x00CF0000) | unchecked((int)0x80000000));
                SetWindowLong(hwnd, ExStyle, originalExStyle | NoActivate | ToolWindow | Layered | Transparent);
                var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                Marshal.ThrowExceptionForHR(DwmExtendFrameIntoClientArea(hwnd, ref margins));
                RefreshBounds();
                Register(1, 'P'); Register(2, 'H'); Register(3, 'Q');
                keyboardIdle = new WindowsKeyboardIdle();
            }
            catch { Dispose(); throw; }
        }

        public void RefreshBounds()
        {
            Bounds = ReadDesktopBounds();
            WorkAreas = ReadDesktopWorkAreas();
            WorkArea = CombineWorkAreas(WorkAreas);
            if (!SetWindowPos(hwnd, new IntPtr(-1), (int)Bounds.X, (int)Bounds.Y, (int)Bounds.Width, (int)Bounds.Height, 0x10 | 0x20))
                throw new Win32Exception();
        }

        public static DesktopRect ReadDesktopBounds()
        {
            // 虚拟桌面包含所有屏幕；左侧或上方副屏的原点可以为负数。
            return new DesktopRect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        }

        public static DesktopRect ReadDesktopWorkArea()
        { return CombineWorkAreas(ReadDesktopWorkAreas()); }

        private static DesktopRect CombineWorkAreas(DesktopRect[] areas)
        {
            var area = areas[0];
            for (int i = 1; i < areas.Length; i++)
            {
                var work = areas[i];
                double left = Math.Min(area.X, work.X), top = Math.Min(area.Y, work.Y);
                double right = Math.Max(area.X + area.Width, work.X + work.Width);
                double bottom = Math.Max(area.Y + area.Height, work.Y + work.Height);
                area = new DesktopRect(left, top, right - left, bottom - top);
            }
            return area;
        }

        public static DesktopRect[] ReadDesktopWorkAreas()
        {
            var areas = new List<DesktopRect>();
            MonitorProc collect = delegate(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data)
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception();
                var work = info.Work;
                areas.Add(new DesktopRect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top));
                return true;
            };
            if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, collect, IntPtr.Zero)) throw new Win32Exception();
            return areas.ToArray();
        }

        public void SetInteractive(bool interactive)
        {
            int current = GetWindowLong(hwnd, ExStyle);
            int wanted = interactive ? current & ~Transparent : current | Transparent;
            if (current != wanted) SetWindowLong(hwnd, ExStyle, wanted);
        }

        public void SetVisible(bool visible) { ShowWindow(hwnd, visible ? 8 : 0); }

        public PetCommand TakeCommand()
        {
            var command = pending;
            pending = PetCommand.None;
            return command;
        }

        public Point2? TakeLeftPress()
        {
            var point = leftPress;
            leftPress = null;
            return point;
        }

        public Point2? TakeLeftRelease()
        {
            var point = leftRelease;
            leftRelease = null;
            return point;
        }

        public int ContextMenu(bool paused, string relationship, string organizing = "", string companion = "", bool companionEnabled = true)
        {
            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) throw new Win32Exception();
            try
            {
                AppendMenu(menu, 2, UIntPtr.Zero, "关系：" + relationship);
                if (!string.IsNullOrEmpty(organizing)) AppendMenu(menu, 2, UIntPtr.Zero, organizing);
                if (!string.IsNullOrEmpty(companion)) AppendMenu(menu, 2, UIntPtr.Zero, "屏幕陪伴：" + companion);
                AppendMenu(menu, 0, new UIntPtr(1), paused ? "继续活动" : "暂停活动");
                AppendMenu(menu, 0, new UIntPtr(2), "隐藏（Ctrl+Alt+H 恢复）");
                AppendMenu(menu, 0, new UIntPtr(4), companionEnabled ? "关闭屏幕陪伴" : "开启屏幕陪伴");
                AppendMenu(menu, 0, new UIntPtr(3), "退出");
                Point p; GetCursorPos(out p);
                return TrackPopupMenu(menu, 0x100 | 0x2, p.X, p.Y, 0, hwnd, IntPtr.Zero);
            }
            finally { DestroyMenu(menu); }
        }

        public static Point2 Cursor()
        {
            Point point;
            if (!GetCursorPos(out point)) throw new Win32Exception();
            return new Point2(point.X, point.Y);
        }

        public static bool ButtonDown(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }

        public static DesktopRect ReadWorkArea()
        {
            Rect work;
            if (!SystemParametersInfo(0x30, 0, out work, 0)) throw new Win32Exception();
            return new DesktopRect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);
        }

        public static bool KeyboardBusy()
        {
            // 只返回键盘是否有键按住，排除鼠标按钮，不读取字符或保存键码。
            for (int key = 8; key < 255; key++)
                if ((GetAsyncKeyState(key) & 0x8000) != 0) return true;
            return false;
        }

        private void Register(int id, char key)
        {
            // 快捷键冲突需立即报告；隐藏后不能依赖不可见的右键菜单恢复。
            if (!RegisterHotKey(hwnd, id, 0x4000 | 0x1 | 0x2, key)) throw new Win32Exception();
        }

        private IntPtr HandleMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == 0x201 || message == 0x202)
            {
                // 消息坐标相对客户端，缓存为全局桌面坐标，与鼠标及文件拖放保持一致。
                long coordinates = lParam.ToInt64();
                var point = new Point2(Bounds.X + unchecked((short)coordinates), Bounds.Y + unchecked((short)(coordinates >> 16)));
                if (message == 0x201) { leftPress = point; leftRelease = null; }
                else leftRelease = point;
                // 桌宠控制器消费左键，不再交给 Unity 产生另一套鼠标行为。
                return IntPtr.Zero;
            }
            if (message == 0x312) { pending = (PetCommand)wParam.ToInt32(); return IntPtr.Zero; }
            if (message == 0x21) return new IntPtr(3); // MA_NOACTIVATE：鼠标点击也不激活窗口。
            return CallWindowProc(previousProc, window, message, wParam, lParam);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (keyboardIdle != null) keyboardIdle.Dispose();
            for (int i = 1; i <= 3; i++) UnregisterHotKey(hwnd, i);
            if (previousProc != IntPtr.Zero) SetWindowLongPtr(hwnd, WndProcIndex, previousProc);
            SetWindowLong(hwnd, Style, originalStyle);
            SetWindowLong(hwnd, ExStyle, originalExStyle);
            GC.KeepAlive(callback);
        }

        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
        private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        private delegate bool MonitorProc(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)] public static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint param, out Rect value, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")] private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string label);
        [DllImport("user32.dll")] private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);
        [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    }
}
