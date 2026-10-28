using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using ElecPet.Windows;

namespace ElecPet.Tests
{
    public static class OleDropRouteProbe
    {
        private static readonly Stopwatch watch = Stopwatch.StartNew();
        private static string readyPath;
        private static uint style;
        private static bool isolated;
        private static bool toggleTransparent;
        private static uint sourceThread;
        private static volatile bool sourceDone;
        private static readonly WindowProc windowCallback = WindowMessage;

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
                isolated = Array.IndexOf(args, "isolated") >= 0;
                toggleTransparent = Array.IndexOf(args, "toggle") >= 0;
                if (args[0] == "target")
                {
                    readyPath = args[1]; style = uint.Parse(args[2]);
                    var owner = new Thread(Target) { IsBackground = true };
                    owner.SetApartmentState(ApartmentState.MTA); owner.Start(); owner.Join();
                    return 0;
                }
                return Source(args[1]);
            }
            catch (Exception error) { Console.WriteLine("ERROR " + error); return 1; }
        }

        private static void Target()
        {
            IntPtr shield = IntPtr.Zero, window = IntPtr.Zero;
            PetFileDropTarget target = null;
            try
            {
                FileDropPoint cursor = new FileDropPoint { X = 280, Y = 220 };
                if (!isolated) GetCursorPos(out cursor);
                int x = cursor.X - 180, y = cursor.Y - 120;
                var cls = new WindowClass { Procedure = windowCallback, Name = "ElecPetOleRouteProbe", Background = new IntPtr(6) };
                if (RegisterClass(ref cls) == 0) throw new System.ComponentModel.Win32Exception();
                shield = CreateWindowEx(0x08000088, cls.Name, "ElecPet OLE 隔离背景", 0x90000000,
                    x, y, 400, 280, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                window = CreateWindowEx(0x08000088 | style, cls.Name, "ElecPet OLE 路由诊断", 0x90000000,
                    x, y, 400, 280, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (shield == IntPtr.Zero || window == IntPtr.Zero) throw new InvalidOperationException("无法创建诊断窗口");
                if ((style & 0x80000) != 0) SetLayeredWindowAttributes(window, 0, 255, 2);
                SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x13);
                Console.WriteLine("TARGET process={0} ownerThread={1} apartment={2} hwnd={3} style=0x{4:X}",
                    Process.GetCurrentProcess().Id, GetCurrentThreadId(), Thread.CurrentThread.GetApartmentState(), window, style);
                Console.WriteLine("TARGET cursor={0},{1} underCursor={2}", cursor.X, cursor.Y, WindowFromPoint(cursor));
                target = new PetFileDropTarget(window);
                {
                    if (target != null) target.SetTarget(true, true);
                    File.WriteAllText(readyPath, window + "|" + shield + "|" + cursor.X + "|" + cursor.Y);
                    bool hovering = false, armed = false;
                    bool toggled = false;
                    while (watch.Elapsed.TotalSeconds < 7)
                    {
                        Message message;
                        while (PeekMessage(out message, IntPtr.Zero, 0, 0, 1))
                        { TranslateMessage(ref message); DispatchMessage(ref message); }
                        if (toggleTransparent && !toggled && watch.Elapsed.TotalSeconds >= .5)
                        {
                            uint interactive = (0x08000088 | style) & ~0x20u;
                            if (IntPtr.Size == 8) SetWindowLongPtr(window, -20, new IntPtr(interactive));
                            else SetWindowLong(window, -20, unchecked((int)interactive));
                            toggled = true; Console.WriteLine("STYLE transparent disabled");
                        }
                        target.SetTarget(true, true);
                        if (target.Hovering != hovering || target.Armed != armed)
                        {
                            hovering = target.Hovering; armed = target.Armed;
                            Console.WriteLine("STATE hover={0} armed={1} seconds={2:F3}", hovering, armed, target.HoverSeconds);
                        }
                        FileDropPoint dropped;
                        var files = target.TakeFiles(out dropped);
                        if (files != null) Console.WriteLine("ACCEPTED count={0} point={1},{2} exists={3}",
                            files.Length, dropped.X, dropped.Y, File.Exists(files[0]));
                        Thread.Sleep(10);
                    }
                }
                Console.WriteLine("TARGET finished");
            }
            catch (Exception error) { Console.WriteLine("TARGET ERROR " + error); }
            finally
            {
                if (target != null) target.Dispose();
                if (window != IntPtr.Zero) DestroyWindow(window);
                if (shield != IntPtr.Zero) DestroyWindow(shield);
            }
        }


        private static int Source(string path)
        {
            Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
            IntPtr data = IntPtr.Zero, source = IntPtr.Zero;
            try
            {
                string[] handles = File.ReadAllText(path).Split('|');
                IntPtr target = new IntPtr(long.Parse(handles[0])), shield = new IntPtr(long.Parse(handles[1]));
                FileDropPoint cursor = new FileDropPoint { X = int.Parse(handles[2]), Y = int.Parse(handles[3]) };
                if (!isolated) GetCursorPos(out cursor);
                IntPtr under = WindowFromPoint(cursor);
                Console.WriteLine("SOURCE process={0} target={1} underCursor={2} shield={3}", Process.GetCurrentProcess().Id, target, under, shield);
                if (under != target && under != shield) throw new InvalidOperationException("指针已离开本轮诊断窗口，停止测试");
                data = FileData(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var dropSource = new ProbeSource(target, shield, isolated, cursor);
                source = Marshal.GetComInterfaceForObject(dropSource, typeof(IProbeSource));
                sourceThread = GetCurrentThreadId();
                var drive = new Thread(() =>
                {
                    // 只投递本进程拖动循环消息；不移动指针，不注入任何鼠标或键盘按键。
                    while (!sourceDone)
                    {
                        FileDropPoint current = cursor;
                        if (!isolated) GetCursorPos(out current);
                        int packed = (current.Y << 16) | (current.X & 0xffff);
                        PostThreadMessage(sourceThread, 0x200, UIntPtr.Zero, new IntPtr(packed)); Thread.Sleep(40);
                    }
                }) { IsBackground = true };
                drive.Start();
                uint effect;
                int result = DoDragDrop(data, source, 1, out effect);
                sourceDone = true; drive.Join(1000);
                Console.WriteLine("SOURCE result=0x{0:X8} effect={1}", result, effect);
                return result == 0x40100 && effect == 1 ? 0 : 2;
            }
            finally
            {
                sourceDone = true;
                if (source != IntPtr.Zero) Marshal.Release(source);
                if (data != IntPtr.Zero) Marshal.Release(data);
                OleUninitialize();
            }
        }

        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        public sealed class ProbeSource : IProbeSource
        {
            private readonly IntPtr target, shield;
            private readonly Stopwatch elapsed = Stopwatch.StartNew();
            private readonly bool isolated;
            private readonly FileDropPoint fixedPoint;
            private uint previous = uint.MaxValue;
            public ProbeSource(IntPtr target, IntPtr shield, bool isolated, FileDropPoint point)
            { this.target = target; this.shield = shield; this.isolated = isolated; fixedPoint = point; }
            public int QueryContinueDrag(bool escape, uint keys)
            {
                FileDropPoint point = fixedPoint;
                if (!isolated) GetCursorPos(out point);
                IntPtr under = WindowFromPoint(point);
                if (escape || (under != target && under != shield))
                {
                    Console.WriteLine("SOURCE cancel elapsed={0:F3} under={1} point={2},{3}", elapsed.Elapsed.TotalSeconds, under, point.X, point.Y);
                    return 0x40101;
                }
                return elapsed.Elapsed.TotalSeconds >= 4 ? 0x40100 : 0;
            }
            public int GiveFeedback(uint effect)
            {
                if (effect != previous) { previous = effect; Console.WriteLine("FEEDBACK effect=" + effect); }
                return 0x40102;
            }
        }

        [ComVisible(true), Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IProbeSource
        {
            [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape, uint keys);
            [PreserveSig] int GiveFeedback(uint effect);
        }

        private static IntPtr FileData(string path)
        {
            var iid = new Guid("0000010E-0000-0000-C000-000000000046"); IntPtr data;
            Marshal.ThrowExceptionForHR(SHCreateDataObject(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, ref iid, out data));
            byte[] text = Encoding.Unicode.GetBytes(path + "\0\0");
            IntPtr memory = GlobalAlloc(2, new UIntPtr((uint)(20 + text.Length))), buffer = GlobalLock(memory);
            for (int i = 0; i < 20; i++) Marshal.WriteByte(buffer, i, 0);
            Marshal.WriteInt32(buffer, 0, 20); Marshal.WriteInt32(buffer, 16, 1);
            Marshal.Copy(text, 0, IntPtr.Add(buffer, 20), text.Length); GlobalUnlock(memory);
            var format = new FORMATETC { cfFormat = 15, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
            var medium = new DataMedium { Type = TYMED.TYMED_HGLOBAL, Value = memory };
            var set = (SetData)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(data), 7 * IntPtr.Size), typeof(SetData));
            Marshal.ThrowExceptionForHR(set(data, ref format, ref medium, true));
            return data;
        }

        [StructLayout(LayoutKind.Sequential)] private struct DataMedium { public TYMED Type; public IntPtr Value, Release; }
        private static IntPtr WindowMessage(IntPtr hwnd, uint message, UIntPtr wparam, IntPtr lparam)
        { return message == 0x84 ? new IntPtr(1) : DefWindowProc(hwnd, message, wparam, lparam); }
        private delegate IntPtr WindowProc(IntPtr window, uint message, UIntPtr wparam, IntPtr lparam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
        {
            public uint Style; public WindowProc Procedure; public int ClassBytes, WindowBytes;
            public IntPtr Instance, Icon, Cursor, Background; public string Menu, Name;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Message
        { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public FileDropPoint Point; public uint Private; }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetData(IntPtr data, ref FORMATETC format,
            ref DataMedium medium, [MarshalAs(UnmanagedType.Bool)] bool release);
        [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
        [DllImport("ole32.dll")] private static extern void OleUninitialize();
        [DllImport("ole32.dll")] private static extern int DoDragDrop(IntPtr data, IntPtr source, uint allowed, out uint effect);
        [DllImport("shell32.dll")] private static extern int SHCreateDataObject(IntPtr folder, uint count, IntPtr children, IntPtr inner, ref Guid iid, out IntPtr data);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr size);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint ex, string cls, string title, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out FileDropPoint point);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(FileDropPoint point);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", EntryPoint = "RegisterClassW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClass(ref WindowClass cls);
        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] private static extern IntPtr DefWindowProc(IntPtr window, uint message, UIntPtr wparam, IntPtr lparam);
    }
}
