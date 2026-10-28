using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ElecPet.Core;

namespace ElecPet.Tests
{
    // 默认只检查内存 DIB；显式可见模式仅捕获本轮新建并核对身份的合成窗口。
    public static class ScreenCaptureChecks
    {
        private static Type observer;
        private static readonly WindowProc fixtureProc = DrawFixture;
        private static bool paintAsPet;
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--visible-fixture-child") { SetProcessDPIAware(); return RunVisibleChild(args[1]); }
                observer = typeof(ScreenCaptureChecks).Assembly.GetType("ElecPet.Windows.WindowsScreenObserver");
                Check(observer != null, "屏幕观察模块尚未实现");
                if (args.Length > 0 && args[0] == "--visible-fixture") { SetProcessDPIAware(); VerifyVisibleFixture(); return 0; }
                Check((bool)Call("ExcludeFromCapture", IntPtr.Zero) == false, "无宠物窗口时必须停止捕获");
                Check(Call("Capture", null, IntPtr.Zero) == null, "空观察对象必须安全跳过");
                VerifyForegroundWatch();
                VerifyPixels();
                VerifyClip();
                VerifyWindowSafety();
                Console.WriteLine("PASS: synthetic GDI pixels, resize, crop, RGBA, black rejection, pet affinity and stale identity; no user screen read.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void VerifyPixels()
        {
            IntPtr dc = CreateCompatibleDC(IntPtr.Zero), bitmap = IntPtr.Zero, previous = IntPtr.Zero, data;
            try
            {
                var info = new BitmapInfo { Size = 40, Width = 1600, Height = -800, Planes = 1, Bits = 32 };
                bitmap = CreateDIBSection(dc, ref info, 0, out data, IntPtr.Zero, 0);
                Check(bitmap != IntPtr.Zero, "无法建立合成 DIB");
                previous = SelectObject(dc, bitmap);
                byte[] bgra = new byte[1600 * 800 * 4];
                for (int y = 0; y < 800; y++)
                    for (int x = 0; x < 1600; x++)
                    {
                        int i = (y * 1600 + x) * 4;
                        bgra[i] = (byte)(x < 800 ? 0 : 255); bgra[i + 2] = (byte)(x < 800 ? 255 : 0);
                        bgra[i + 1] = (byte)(y >= 400 ? 255 : 0);
                    }
                Marshal.Copy(bgra, 0, data, bgra.Length);
                object pixels = Call("CapturePixels", dc, new DesktopRect(0, 0, 1600, 800));
                Check(pixels != null, "合成红蓝画面不可被当作黑图");
                Check(Field<int>(pixels, "Width") == 1280 && Field<int>(pixels, "Height") == 640, "最长边 1280 必须保持比例");
                byte[] rgba = Field<byte[]>(pixels, "Rgba");
                Check(rgba[0] == 255 && rgba[1] == 255 && rgba[2] == 0 && rgba[3] == 255, "首行必须对应画面底部，匹配 Unity 左下原点");
                int top = 639 * 1280 * 4;
                Check(rgba[top] == 255 && rgba[top + 1] == 0 && rgba[top + 2] == 0 && rgba[top + 3] == 255, "红色 BGRA 必须转换为 RGBA 并填充不透明 alpha");
                int right = (1279 + 639 * 1280) * 4;
                Check(rgba[right] == 0 && rgba[right + 1] == 0 && rgba[right + 2] == 255 && rgba[right + 3] == 255, "蓝色不能反转");
                object cropped = Call("CapturePixels", dc, new DesktopRect(1000, 200, 200, 100));
                Check(Field<int>(cropped, "Width") == 200 && Field<int>(cropped, "Height") == 100, "小窗口不应放大");
                Check(Field<byte[]>(cropped, "Rgba")[2] == 255, "裁剪必须使用源坐标");
                Array.Clear(bgra, 0, bgra.Length); Marshal.Copy(bgra, 0, data, bgra.Length);
                Check(Call("CapturePixels", dc, new DesktopRect(0, 0, 1600, 800)) == null, "黑图应跳过");
            }
            finally
            {
                if (previous != IntPtr.Zero) SelectObject(dc, previous);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (dc != IntPtr.Zero) DeleteDC(dc);
            }
        }

        private static void VerifyClip()
        {
            var clipped = (DesktopRect)Call("ClipToPrimary", new DesktopRect(-100, -40, 500, 300), 1920, 1080);
            Check(clipped.X == 0 && clipped.Y == 0 && clipped.Width == 400 && clipped.Height == 260, "主屏左上交集错误");
            clipped = (DesktopRect)Call("ClipToPrimary", new DesktopRect(1800, 1000, 500, 300), 1920, 1080);
            Check(clipped.X == 1800 && clipped.Y == 1000 && clipped.Width == 120 && clipped.Height == 80, "主屏右下交集错误");
            clipped = (DesktopRect)Call("ClipToPrimary", new DesktopRect(1920, 0, 500, 300), 1920, 1080);
            Check(clipped.Width == 0, "完全位于副屏时不可观察");
        }

        private static void VerifyWindowSafety()
        {
            Exception failure = null;
            // 不切换输入桌面；这个线程和窗口只存在于本轮独占的后台桌面。
            var thread = new Thread(delegate()
            {
                IntPtr desktop = CreateDesktop("ElecPetCaptureCheck-" + Guid.NewGuid().ToString("N"), null, IntPtr.Zero, 0, 0xc3, IntPtr.Zero);
                IntPtr previous = GetThreadDesktop(GetCurrentThreadId()), pet = IntPtr.Zero;
                try
                {
                    Check(desktop != IntPtr.Zero, "无法建立独占后台桌面：" + Marshal.GetLastWin32Error());
                    Check(SetThreadDesktop(desktop), "无法绑定独占后台桌面：" + Marshal.GetLastWin32Error());
                    pet = CreateWindowEx(0x080800a8, "STATIC", "合成宠物", 0x90000000, 0, 0, 500, 300,
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    Check(pet != IntPtr.Zero, "无法建立合成宠物窗口");
                    SetLayeredWindowAttributes(pet, 0, 200, 2);
                    bool excluded = (bool)Call("ExcludeFromCapture", pet);
                    uint affinity;
                    Check(excluded && GetWindowDisplayAffinity(pet, out affinity) && affinity == 0x11,
                        "合成宠物窗口必须真正设置 WDA_EXCLUDEFROMCAPTURE");
                    Console.WriteLine("PASS: real layered pet HWND excludes capture on isolated desktop.");
                    Type observed = typeof(ScreenCaptureChecks).Assembly.GetType("ElecPet.Windows.ObservedWindow");
                    var bounds = new DesktopRect(0, 0, 500, 300);
                    object original = Activator.CreateInstance(observed, pet, "old", bounds);
                    object renamed = Activator.CreateInstance(observed, pet, "new", bounds);
                    object switched = Activator.CreateInstance(observed, new IntPtr(pet.ToInt64() + 1), "old", bounds);
                    Check(!(bool)Call("SameWindow", original, renamed), "标题变化必须使旧截图失效");
                    Check(!(bool)Call("SameWindow", original, switched), "前台窗口变化必须使旧截图失效");
                    Check(!(bool)Call("SameWindow", original, null), "无前台窗口必须使旧截图失效");
                    // 自身进程窗口或后台无前台的情况必定拒绝；这里永远不会走到屏幕 DC。
                    Check(Call("Capture", original, pet) == null, "宠物不能观察自身进程的窗口");
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    if (pet != IntPtr.Zero) DestroyWindow(pet);
                    if (previous != IntPtr.Zero) SetThreadDesktop(previous);
                    if (desktop != IntPtr.Zero) CloseDesktop(desktop);
                }
            });
            thread.Start();
            Check(thread.Join(5000), "后台窗口检查超时");
            if (failure != null) throw failure;
        }

        private static void VerifyForegroundWatch()
        {
            Type watchType = typeof(ScreenCaptureChecks).Assembly.GetType("ElecPet.Windows.ForegroundWatch");
            Check(watchType != null, "缺少前台事件监听，快速切换后返回会漏判");
            object watch = Activator.CreateInstance(watchType);
            var disposable = (IDisposable)watch;
            try
            {
                Check((bool)watchType.GetProperty("Active").GetValue(watch, null), "真实前台事件钩子必须成功注册");
                MethodInfo callback = watchType.GetMethod("OnWinEvent", BindingFlags.NonPublic | BindingFlags.Instance);
                long before = (long)watchType.GetProperty("Revision").GetValue(watch, null);
                Action<uint, IntPtr, int, int> fire = delegate(uint kind, IntPtr window, int objectId, int childId)
                { callback.Invoke(watch, new object[] { IntPtr.Zero, kind, window, objectId, childId, (uint)0, (uint)0 }); };
                fire(3, new IntPtr(1), 0, 0); fire(3, new IntPtr(2), 0, 0);
                Check((long)watchType.GetProperty("Revision").GetValue(watch, null) == before + 2,
                    "连续两次前台切换必须各自增加版本，即使轮询前已经返回");
                IntPtr foreground = GetForegroundWindow();
                fire(0x800c, foreground, -4, 0); fire(0x800c, foreground, 0, 1);
                fire(0x800c, IntPtr.Zero, 0, 0); fire(0x800c, new IntPtr(-1), 0, 0);
                Check((long)watchType.GetProperty("Revision").GetValue(watch, null) == before + 2,
                    "子控件、子对象和非前台窗口名称事件不应重置停留");
                if (foreground != IntPtr.Zero)
                {
                    fire(0x800c, foreground, 0, 0);
                    Check((long)watchType.GetProperty("Revision").GetValue(watch, null) == before + 3,
                        "前台顶层窗口标题事件必须增加版本");
                }
                disposable.Dispose();
                Check(!(bool)watchType.GetProperty("Active").GetValue(watch, null), "释放后必须关闭事件监听");
                long stopped = (long)watchType.GetProperty("Revision").GetValue(watch, null);
                fire(3, new IntPtr(3), 0, 0);
                Check((long)watchType.GetProperty("Revision").GetValue(watch, null) == stopped, "释放后的迟到回调不能继续改变状态");
                Console.WriteLine("PASS: foreground hook registration, rapid switches, title event filters and disposal.");
            }
            finally { disposable.Dispose(); }
        }

        private static int RunVisibleChild(string title)
        {
            RegisterFixtureClass(title);
            IntPtr window = CreateWindowEx(0, title, title, 0x90000000, 80, 80, 800, 500,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Check(window != IntPtr.Zero, "无法建立可见合成窗口");
            UpdateWindow(window); SetForegroundWindow(window);
            Console.WriteLine(window.ToInt64()); Console.Out.Flush();
            Message message;
            while (GetMessage(out message, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref message); DispatchMessage(ref message); }
            return 0;
        }

        private static void VerifyVisibleFixture()
        {
            // 只有显式模式会前置本轮新建窗口；捕获前确认唯一标题、PID、句柄和边界，像素仅保留在内存。
            string title = "ElecPetVisibleCapture-" + Guid.NewGuid().ToString("N");
            IntPtr previous = GetForegroundWindow(), target = IntPtr.Zero, pet = IntPtr.Zero;
            Process child = null;
            try
            {
                Check(GetSystemMetrics(0) >= 900 && GetSystemMetrics(1) >= 650, "主屏不足以完整容纳合成窗口，检查已中止");
                child = Process.Start(new ProcessStartInfo(typeof(ScreenCaptureChecks).Assembly.Location,
                    "--visible-fixture-child " + title) { UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true });
                string line = child.StandardOutput.ReadLine();
                long handle;
                Check(long.TryParse(line, out handle) && handle != 0, "合成子进程未返回有效窗口");
                target = new IntPtr(handle);
                uint process;
                GetWindowThreadProcessId(target, out process);
                Check(process == child.Id, "合成窗口进程身份不符，检查已中止");
                SetForegroundWindow(target);
                Thread.Sleep(180);
                Check(GetForegroundWindow() == target, "合成窗口未取得前台或已切走，检查已中止");
                paintAsPet = true; RegisterFixtureClass(title + "-Pet");
                pet = CreateWindowEx(0x080800a8, title + "-Pet", "合成宠物", 0x90000000, 180, 180, 120, 100,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                Check(pet != IntPtr.Zero, "无法建立可见合成宠物");
                SetLayeredWindowAttributes(pet, 0, 255, 2); UpdateWindow(pet);
                Check((bool)Call("ExcludeFromCapture", pet), "合成宠物不能排除捕获，检查已中止");
                DwmFlush(); Thread.Sleep(100);
                object observed = Call("ReadForeground", pet);
                DiagnoseFixture(target, title, child.Id, observed);
                Check(observed != null && Field<IntPtr>(observed, "Handle") == target &&
                    Field<string>(observed, "Identity").EndsWith(":" + title, StringComparison.Ordinal),
                    "观察对象不是本轮合成窗口，检查已中止");
                DesktopRect bounds = Field<DesktopRect>(observed, "Bounds");
                Check(bounds.X == 80 && bounds.Y == 80 && bounds.Width == 800 && bounds.Height == 500,
                    "合成窗口边界已改变，检查已中止");
                Check(GetForegroundWindow() == target, "捕获前窗口已切走，检查已中止");
                object pixels = Call("Capture", observed, pet);
                Check(pixels != null && GetForegroundWindow() == target, "捕获失败或已切换窗口，检查已中止");
                Check(Field<int>(pixels, "Width") == 800 && Field<int>(pixels, "Height") == 500, "实际窗口截图尺寸错误");
                byte[] rgba = Field<byte[]>(pixels, "Rgba");
                VerifyColor(rgba, 100, 400, 255, 0, 0, "左上红色");
                VerifyColor(rgba, 700, 400, 0, 0, 255, "右上蓝色");
                VerifyColor(rgba, 100, 80, 255, 255, 0, "左下黄色及行序");
                VerifyColor(rgba, 700, 80, 0, 255, 255, "右下青色及行序");
                VerifyColor(rgba, 150, 350, 255, 0, 0, "宠物覆盖区域应恢复为底层红色");
                Console.WriteLine("PASS: visible cross-process fixture captured at 800x500; RGBA, orientation, target identity and layered pet pixel exclusion verified; no images saved.");
            }
            finally
            {
                if (pet != IntPtr.Zero) DestroyWindow(pet);
                if (target != IntPtr.Zero) PostMessage(target, 0x10, IntPtr.Zero, IntPtr.Zero);
                if (child != null) { if (!child.WaitForExit(2000)) { child.Kill(); child.WaitForExit(2000); } child.Dispose(); }
                if (previous != IntPtr.Zero && IsWindow(previous)) SetForegroundWindow(previous);
            }
        }

        private static void DiagnoseFixture(IntPtr target, string title, int childProcess, object observed)
        {
            // 诊断只读取本轮合成 HWND；标题仅输出长度与匹配结果，绝不输出用户窗口标题。
            uint process;
            GetWindowThreadProcessId(target, out process);
            int titleLength = GetWindowTextLength(target), cloaked;
            var actualTitle = new StringBuilder(titleLength + 1);
            GetWindowText(target, actualTitle, actualTitle.Capacity);
            PaintRect raw, frame;
            bool hasRaw = GetWindowRect(target, out raw);
            int frameResult = DwmGetWindowAttribute(target, 9, out frame, Marshal.SizeOf(typeof(PaintRect)));
            int cloakResult = DwmGetWindowAttribute(target, 14, out cloaked, sizeof(int));
            Console.WriteLine("DIAG fixture: foregroundMatches={0}; pidMatches={1}; inputDesktopAvailable={2}; visible={3}; iconic={4}; cloakHr={5}; cloaked={6}; titleLen={7}; expectedTitleLen={8}; titleMatches={9}; observedNull={10}; observedHandleMatches={11}; observedTitleMatches={12}",
                GetForegroundWindow() == target, process == childProcess, Call("InputDesktopAvailable"),
                IsWindowVisible(target), IsIconic(target), cloakResult, cloaked, titleLength, title.Length,
                actualTitle.ToString() == title, observed == null,
                observed != null && Field<IntPtr>(observed, "Handle") == target,
                observed != null && Field<string>(observed, "Identity").EndsWith(":" + title, StringComparison.Ordinal));
            Console.WriteLine("DIAG fixture bounds: rawAvailable={0}; raw={1},{2},{3},{4}; frameHr={5}; frame={6},{7},{8},{9}",
                hasRaw, raw.Left, raw.Top, raw.Right, raw.Bottom, frameResult, frame.Left, frame.Top, frame.Right, frame.Bottom);
        }

        private static void VerifyColor(byte[] rgba, int x, int y, int red, int green, int blue, string reason)
        {
            int index = (y * 800 + x) * 4;
            Check(Math.Abs(rgba[index] - red) < 12 && Math.Abs(rgba[index + 1] - green) < 12 &&
                Math.Abs(rgba[index + 2] - blue) < 12 && rgba[index + 3] == 255, reason + "错误");
        }
        private static void RegisterFixtureClass(string name)
        {
            var windowClass = new WindowClass { Size = (uint)Marshal.SizeOf(typeof(WindowClass)), Callback = fixtureProc,
                Instance = GetModuleHandle(null), Name = name };
            Check(RegisterClassEx(ref windowClass) != 0, "无法注册合成窗口类");
        }
        private static IntPtr DrawFixture(IntPtr window, uint message, IntPtr wparam, IntPtr lparam)
        {
            if (message == 0x0f)
            {
                Paint paint;
                IntPtr dc = BeginPaint(window, out paint);
                if (paintAsPet) FillColor(dc, new PaintRect(0, 0, 120, 100), 0x00ff00);
                else
                {
                    FillColor(dc, new PaintRect(0, 0, 400, 250), 0x0000ff);
                    FillColor(dc, new PaintRect(400, 0, 800, 250), 0xff0000);
                    FillColor(dc, new PaintRect(0, 250, 400, 500), 0x00ffff);
                    FillColor(dc, new PaintRect(400, 250, 800, 500), 0xffff00);
                }
                EndPaint(window, ref paint); return IntPtr.Zero;
            }
            if (message == 2) { PostQuitMessage(0); return IntPtr.Zero; }
            return DefWindowProc(window, message, wparam, lparam);
        }
        private static void FillColor(IntPtr dc, PaintRect rect, uint color)
        {
            IntPtr brush = CreateSolidBrush(color);
            try { FillRect(dc, ref rect, brush); }
            finally { DeleteObject(brush); }
        }
        private static object Call(string name, params object[] args)
        { return observer.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args); }
        private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name).GetValue(value); }
        private static void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
        { public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize; public int XPixels, YPixels; public uint Colors, Important; }
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateDesktop(string name, string device, IntPtr mode, uint flags, uint access, IntPtr security);
        [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetThreadDesktop(IntPtr desktop);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint extended, string name, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr data);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
        [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        private delegate IntPtr WindowProc(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
        { public uint Size, Style; public WindowProc Callback; public int ClassExtra, WindowExtra; public IntPtr Instance, Icon, Cursor, Background; public string Menu, Name; public IntPtr SmallIcon; }
        [StructLayout(LayoutKind.Sequential)] private struct Message
        { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
        [StructLayout(LayoutKind.Sequential)] private struct PaintRect
        { public int Left, Top, Right, Bottom; public PaintRect(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; } }
        [StructLayout(LayoutKind.Sequential)] private struct Paint
        { public IntPtr DC; public int Erase; public PaintRect Rect; public int Restore, Update; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WindowClass value);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] private static extern int GetMessage(out Message message, IntPtr window, uint first, uint last);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
        [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr window, out Paint paint);
        [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr window, ref Paint paint);
        [DllImport("user32.dll")] private static extern int FillRect(IntPtr dc, ref PaintRect rect, IntPtr brush);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
        [DllImport("dwmapi.dll")] private static extern int DwmFlush();
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder title, int capacity);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out PaintRect rect);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out PaintRect value, int length);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int length);
    }
}
