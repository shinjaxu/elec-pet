using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ElecPet.Core;

namespace ElecPet.Windows
{
    // 由主线程注册并接收 Windows 消息，记录轮询间隙的快速切换和标题变化。
    public sealed class ForegroundWatch : IDisposable
    {
        private readonly WinEventProc callback;
        private IntPtr foregroundHook, nameHook;
        private long revision;
        private volatile bool active;
        public long Revision { get { return Interlocked.Read(ref revision); } }
        public bool Active { get { return active; } }

        public ForegroundWatch()
        {
            callback = OnWinEvent;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            foregroundHook = SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0);
            nameHook = SetWinEventHook(0x800c, 0x800c, IntPtr.Zero, callback, 0, 0, 0);
            active = foregroundHook != IntPtr.Zero && nameHook != IntPtr.Zero;
            if (!active) Dispose();
        }

        private void OnWinEvent(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time)
        {
            if (!active) return;
            // 前台切换无条件失效；名称事件仅认当前前台顶层窗口，忽略正文和子控件变化。
            if (kind == 3 || (kind == 0x800c && objectId == 0 && childId == 0 && window != IntPtr.Zero &&
                window == GetForegroundWindow() && GetAncestor(window, 2) == window)) Interlocked.Increment(ref revision);
        }

        public void Dispose()
        {
            active = false;
            if (foregroundHook != IntPtr.Zero) { UnhookWinEvent(foregroundHook); foregroundHook = IntPtr.Zero; }
            if (nameHook != IntPtr.Zero) { UnhookWinEvent(nameHook); nameHook = IntPtr.Zero; }
        }
        private delegate void WinEventProc(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time);
        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    }

    public sealed class ObservedWindow
    {
        public readonly IntPtr Handle;
        public readonly string Identity;
        public readonly DesktopRect Bounds;
        public ObservedWindow(IntPtr handle, string identity, DesktopRect bounds)
        { Handle = handle; Identity = identity; Bounds = bounds; }
    }

    public sealed class ScreenPixels
    {
        public readonly int Width, Height;
        // 底行优先的 RGBA，与 Unity 纹理左下原点一致，可直接加载并编码图片。
        public readonly byte[] Rgba;
        public ScreenPixels(int width, int height, byte[] rgba)
        { Width = width; Height = height; Rgba = rgba; }
    }

    public static class WindowsScreenObserver
    {
        // 观察对象由窗口、进程和标题共同确定；标题只用于内存内失效判断，不输出到日志。
        public static ObservedWindow ReadForeground(IntPtr petWindow)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || !InputDesktopAvailable()) return null;
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero || window == petWindow || window == GetShellWindow() || window == GetDesktopWindow() ||
                !IsWindowVisible(window) || IsIconic(window)) return null;
            uint process;
            if (GetWindowThreadProcessId(window, out process) == 0 || process == GetCurrentProcessId()) return null;
            var className = new StringBuilder(256);
            GetClassName(window, className, className.Capacity);
            string name = className.ToString();
            if (name == "Progman" || name == "WorkerW" || name == "Shell_TrayWnd" || name == "Shell_SecondaryTrayWnd") return null;
            int cloaked;
            if (DwmGetWindowAttribute(window, 14, out cloaked, sizeof(int)) == 0 && cloaked != 0) return null;
            Rect rect;
            // 排除 GetWindowRect 可能包含的不可见缩放边框，避免带入相邻窗口的像素。
            if (DwmGetWindowAttribute(window, 9, out rect, Marshal.SizeOf(typeof(Rect))) != 0 && !GetWindowRect(window, out rect)) return null;
            DesktopRect bounds = ClipToPrimary(new DesktopRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
                GetSystemMetrics(0), GetSystemMetrics(1));
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;
            var title = new StringBuilder(GetWindowTextLength(window) + 1);
            GetWindowText(window, title, title.Capacity);
            return new ObservedWindow(window, window.ToInt64().ToString("X") + ":" + process + ":" + title, bounds);
        }

        // 先确认宠物可排除，再检查前台身份；捕获前后任意变化都丢弃，避免对旧页面发表回复。
        public static ScreenPixels Capture(ObservedWindow expected, IntPtr petWindow)
        {
            if (expected == null || !ExcludeFromCapture(petWindow) || !SameWindow(expected, ReadForeground(petWindow))) return null;
            IntPtr screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) return null;
            ScreenPixels pixels;
            try { pixels = CapturePixels(screen, expected.Bounds); }
            finally { ReleaseDC(IntPtr.Zero, screen); }
            return SameWindow(expected, ReadForeground(petWindow)) ? pixels : null;
        }

        // 旧版 Windows 会把 0x11 降级为黑色遮挡；必须确认系统版本及 DWM，不能把整屏宠物黑层当作用户画面。
        public static bool ExcludeFromCapture(IntPtr petWindow)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || petWindow == IntPtr.Zero) return false;
            var version = new VersionInfo { Size = Marshal.SizeOf(typeof(VersionInfo)) };
            bool composing;
            uint affinity;
            return RtlGetVersion(ref version) == 0 && (version.Major > 10 || (version.Major == 10 && version.Build >= 19041)) &&
                DwmIsCompositionEnabled(out composing) == 0 && composing && SetWindowDisplayAffinity(petWindow, 0x11) &&
                GetWindowDisplayAffinity(petWindow, out affinity) && affinity == 0x11;
        }

        private static bool InputDesktopAvailable()
        {
            IntPtr desktop = OpenInputDesktop(0, false, 1);
            if (desktop == IntPtr.Zero) return false;
            try
            {
                var name = new StringBuilder(256);
                uint needed;
                return GetUserObjectInformation(desktop, 2, name, name.Capacity * 2, out needed) && name.ToString() == "Default";
            }
            finally { CloseDesktop(desktop); }
        }

        private static bool SameWindow(ObservedWindow expected, ObservedWindow current)
        {
            return current != null && expected.Identity == current.Identity && expected.Handle == current.Handle &&
                expected.Bounds.X == current.Bounds.X && expected.Bounds.Y == current.Bounds.Y &&
                expected.Bounds.Width == current.Bounds.Width && expected.Bounds.Height == current.Bounds.Height;
        }

        private static DesktopRect ClipToPrimary(DesktopRect rect, int width, int height)
        {
            double left = Math.Max(0, rect.X), top = Math.Max(0, rect.Y);
            return new DesktopRect(left, top, Math.Max(0, Math.Min(width, rect.X + rect.Width) - left),
                Math.Max(0, Math.Min(height, rect.Y + rect.Height) - top));
        }

        private static ScreenPixels CapturePixels(IntPtr source, DesktopRect bounds)
        {
            double scale = Math.Min(1, 1280 / Math.Max(bounds.Width, bounds.Height));
            int width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
            int height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
            IntPtr target = CreateCompatibleDC(source), bitmap = IntPtr.Zero, previous = IntPtr.Zero, data;
            if (target == IntPtr.Zero) return null;
            try
            {
                var info = new BitmapInfo { Size = 40, Width = width, Height = height, Planes = 1, Bits = 32 };
                bitmap = CreateDIBSection(target, ref info, 0, out data, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero || data == IntPtr.Zero) return null;
                previous = SelectObject(target, bitmap);
                if (previous == IntPtr.Zero || previous == new IntPtr(-1)) return null;
                SetStretchBltMode(target, 4);
                if (!StretchBlt(target, 0, 0, width, height, source, (int)bounds.X, (int)bounds.Y,
                    (int)bounds.Width, (int)bounds.Height, 0x40cc0020)) return null;
                GdiFlush();
                byte[] rgba = new byte[width * height * 4];
                Marshal.Copy(data, rgba, 0, rgba.Length);
                bool hasColor = false;
                for (int i = 0; i < rgba.Length; i += 4)
                {
                    byte blue = rgba[i]; rgba[i] = rgba[i + 2]; rgba[i + 2] = blue; rgba[i + 3] = 255;
                    hasColor |= (rgba[i] | rgba[i + 1] | blue) != 0;
                }
                return hasColor ? new ScreenPixels(width, height, rgba) : null;
            }
            finally
            {
                if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(target, previous);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                DeleteDC(target);
            }
        }

        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
        { public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize; public int XPixels, YPixels; public uint Colors, Important; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct VersionInfo
        { public int Size, Major, Minor, Build, Platform; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string ServicePack; }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
        [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);
        [DllImport("user32.dll")] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int length, out uint needed);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int length);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int length);
        [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out bool enabled);
        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)] private static extern int RtlGetVersion(ref VersionInfo version);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr dc, int mode);
        [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint operation);
        [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    }
}
