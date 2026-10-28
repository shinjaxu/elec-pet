using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ElecPet.Windows;

namespace ElecPet.Tests
{
    public static class WindowsDropChecks
    {
        private static Type hoverType;
        private static object hover;
        private static int checks;

        [MTAThread]
        public static int Main()
        {
            try
            {
                hoverType = Assembly.GetExecutingAssembly().GetType("ElecPet.Windows.FileDropHover");
                Check(hoverType != null, "RED: 尚未实现真实文件拖动的连续悬停规则。");
                Check(hoverType.GetProperty("DraggingFiles") != null, "真实文件拖动状态应独立于宠物命中，避免被当成提起。");
                hover = Activator.CreateInstance(hoverType, true);
                Call("SetTarget", true, true, 0.0);
                Check(!Flag("Hovering") && Seconds(5) == 0, "普通鼠标悬停不能开始计时。");
                Call("Enter", false, 5.0);
                Check(!Flag("Hovering"), "非文件拖动不能开始计时。");
                Call("Enter", true, 10.0);
                Check(Flag("Hovering") && Seconds(12.9) < 3, "真实文件在宠物上悬停才累计时间。");
                Check(!(bool)Call("Drop", 12.999), "不足3秒松手不得提交文件。");
                Check(!Flag("Hovering") && Seconds(14) == 0, "松手后清除悬停状态。");
                Call("Enter", true, 20.0);
                Check((bool)Call("Drop", 23.0), "连续满3秒的真实Drop允许提交。");
                Check(!(bool)Call("Drop", 24.0), "同一次Drop不能重复提交。");
                Call("Enter", true, 30.0);
                Call("SetTarget", false, true, 32.0);
                Call("SetTarget", true, true, 33.0);
                Check(Seconds(35.9) < 3 && !(bool)Call("Drop", 35.9), "离开宠物区域后必须重新等待3秒。");
                Call("Enter", true, 40.0);
                Call("SetTarget", true, false, 42.0);
                Check(!Flag("Hovering"), "暂停或忙碌立即清零。");
                Check(Flag("DraggingFiles"), "暂不可接收时仍应知道用户正拖着文件，不能误当提起宠物。");
                Call("SetTarget", true, true, 50.0);
                Check(!(bool)Call("Drop", 52.9), "恢复后不能继承暂停前时间。");
                Call("Enter", true, 60.0);
                Call("Leave");
                Check(!Flag("Hovering") && !(bool)Call("Drop", 65.0), "OLE DragLeave 后绝不接受迟到Drop。");
                Console.WriteLine("PASS: {0} 核心悬停规则；开始原生拖放检查", checks);
                NativeCallbacks();
                Console.WriteLine("PASS: {0} 文件拖动悬停检查", checks);
                return 0;
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        }

        private static object Call(string name, params object[] args)
        { return hoverType.GetMethod(name).Invoke(hover, args); }
        private static bool Flag(string name)
        { return (bool)hoverType.GetProperty(name).GetValue(hover, null); }
        private static double Seconds(double now) { return (double)Call("Seconds", now); }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }

        private static void NativeCallbacks()
        {
            Check(Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA, "验证窗口属于MTA主线程。");
            IntPtr window = CreateWindowEx(0, "STATIC", "ElecPet 拖放测试", 0, 0, 0, 10, 10, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Check(window != IntPtr.Zero, "创建仅供本次检查使用的隐藏窗口。");
            try
            {
                using (var target = new PetFileDropTarget(window))
                {
                    bool duplicated = false;
                    try { using (var second = new PetFileDropTarget(window)) { } }
                    catch (InvalidOperationException) { duplicated = true; }
                    Check(duplicated, "独立STA已在其他线程的窗口成功注册原生IDropTarget。");
                    string file = Assembly.GetExecutingAssembly().Location;
                    IntPtr data = FileData(file);
                    IntPtr callback = Marshal.GetComInterfaceForObject(target, typeof(IFileDropTarget));
                    try
                    {
                        var enter = (EnterDrop)Method(callback, 3, typeof(EnterDrop));
                        var over = (OverDrop)Method(callback, 4, typeof(OverDrop));
                        var leave = (LeaveDrop)Method(callback, 5, typeof(LeaveDrop));
                        var drop = (EnterDrop)Method(callback, 6, typeof(EnterDrop));
                        var point = new FileDropPoint();
                        uint effect = 1;
                        target.SetTarget(true, true);
                        Check(enter(callback, data, 0, point, ref effect) == 0 && target.Hovering && effect == 1,
                            "原生COM虚表DragEnter能解析CF_HDROP，且只读取路径。");
                        Check(target.PreviewExtension == ".exe", "预览扩展名不读取文件内容。");
                        effect = 1;
                        Check(over(callback, 0, point, ref effect) == 0 && effect == 1 && !target.Armed,
                            "原生DragOver持续提供文件拖动反馈但不提前准备完成。");
                        effect = 1; drop(callback, data, 0, point, ref effect);
                        Check(effect == 0 && target.TakeFiles() == null, "原生Drop不足3秒返回DROPEFFECT_NONE。");
                        effect = 1; enter(callback, data, 0, point, ref effect);
                        leave(callback);
                        Check(!target.Hovering && !target.Armed, "原生DragLeave清零准备状态。");
                        effect = 1; enter(callback, data, 0, point, ref effect);
                        Thread.Sleep(3050);
                        Check(target.Armed && target.TakeFiles() == null, "悬停满3秒仍不提前提交文件。");
                        effect = 1; drop(callback, data, 0, point, ref effect);
                        var files = target.TakeFiles();
                        Check(effect == 1 && files != null && files.Length == 1 && files[0] == file,
                            "原生Drop满3秒后只发布路径，不移动文件。");
                        Check(target.TakeFiles() == null && System.IO.File.Exists(file), "发布只消费一次，源文件仍存在。");
                        target.SetTarget(true, true);
                        effect = 1; enter(callback, data, 0, point, ref effect);
                        Thread.Sleep(3050);
                        point.X = 1234; point.Y = 567;
                        effect = 1; drop(callback, data, 0, point, ref effect);
                        target.SetTarget(false, true);
                        FileDropPoint savedPoint;
                        Check(target.TakeFiles(out savedPoint) != null, "Drop后再移开鼠标，不能丢弃已经接受的投喂。");
                        Check(savedPoint.X == 1234 && savedPoint.Y == 567, "保留真实Drop屏幕坐标，供主线程核对宠物碰撞体。");
                        effect = 1; enter(callback, IntPtr.Zero, 0, point, ref effect);
                        Check(effect == 0 && !target.Hovering, "不支持CF_HDROP的拖动无法准备喂食。");
                    }
                    finally { Marshal.Release(callback); Marshal.Release(data); }
                }
                using (var registeredAgain = new PetFileDropTarget(window))
                    Check(!registeredAgain.Hovering, "Dispose已注销目标，同一窗口可重新注册。");
            }
            finally { DestroyWindow(window); }
        }

        private static Delegate Method(IntPtr target, int slot, Type type)
        { return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(target), slot * IntPtr.Size), type); }

        private static IntPtr FileData(string path)
        {
            // 使用 Shell 自己的 IDataObject，避免测试源依赖运行时的托管 COM 导出实现。
            var iid = new Guid("0000010E-0000-0000-C000-000000000046");
            IntPtr data;
            Marshal.ThrowExceptionForHR(SHCreateDataObject(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, ref iid, out data));
            byte[] text = Encoding.Unicode.GetBytes(path + "\0\0");
            IntPtr memory = GlobalAlloc(2, new UIntPtr((uint)(20 + text.Length)));
            IntPtr buffer = GlobalLock(memory);
            Marshal.WriteInt32(buffer, 0, 20); Marshal.WriteInt32(buffer, 4, 0); Marshal.WriteInt32(buffer, 8, 0);
            Marshal.WriteInt32(buffer, 12, 0); Marshal.WriteInt32(buffer, 16, 1);
            Marshal.Copy(text, 0, IntPtr.Add(buffer, 20), text.Length); GlobalUnlock(memory);
            var setData = (SetData)Method(data, 7, typeof(SetData));
            int formatSize = IntPtr.Size == 8 ? 32 : 20, mediumSize = IntPtr.Size == 8 ? 24 : 12;
            IntPtr format = Marshal.AllocHGlobal(formatSize), medium = Marshal.AllocHGlobal(mediumSize);
            int result;
            try
            {
                for (int offset = 0; offset < formatSize; offset++) Marshal.WriteByte(format, offset, 0);
                for (int offset = 0; offset < mediumSize; offset++) Marshal.WriteByte(medium, offset, 0);
                Marshal.WriteInt16(format, 0, 15);
                Marshal.WriteInt32(format, IntPtr.Size == 8 ? 16 : 8, 1);
                Marshal.WriteInt32(format, IntPtr.Size == 8 ? 20 : 12, -1);
                Marshal.WriteInt32(format, IntPtr.Size == 8 ? 24 : 16, 1);
                Marshal.WriteInt32(medium, 0, 1); Marshal.WriteIntPtr(medium, IntPtr.Size, memory);
                result = setData(data, format, medium, true);
            }
            finally { Marshal.FreeHGlobal(format); Marshal.FreeHGlobal(medium); }
            if (result < 0) { GlobalFree(memory); Marshal.Release(data); Marshal.ThrowExceptionForHR(result); }
            return data;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetData(IntPtr data, IntPtr format, IntPtr medium, [MarshalAs(UnmanagedType.Bool)] bool release);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EnterDrop(IntPtr target, IntPtr data, uint keys, FileDropPoint point, ref uint effect);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OverDrop(IntPtr target, uint keys, FileDropPoint point, ref uint effect);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LeaveDrop(IntPtr target);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className, string name, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("shell32.dll")] private static extern int SHCreateDataObject(IntPtr folder, uint count, IntPtr children,
            IntPtr inner, ref Guid iid, out IntPtr data);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr size);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
    }
}
