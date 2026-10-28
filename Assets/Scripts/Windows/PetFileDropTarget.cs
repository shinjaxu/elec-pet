using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ElecPet.Windows
{
    // 只有 OLE 确认的文件拖动能计时；离开、暂停、忙碌和松手都会打断连续悬停。
    internal sealed class FileDropHover
    {
        private bool dragging, hit, available;
        private double entered;
        public bool Hovering { get; private set; }
        public bool DraggingFiles { get { return dragging; } }
        public void SetTarget(bool hit, bool available, double now)
        {
            this.hit = hit; this.available = available;
            bool hovering = dragging && hit && available;
            if (hovering && !Hovering) entered = now;
            Hovering = hovering;
        }
        public void Enter(bool files, double now)
        { Leave(); dragging = files; SetTarget(hit, available, now); }
        public double Seconds(double now) { return Hovering ? Math.Max(0, now - entered) : 0; }
        public bool Drop(double now)
        { bool accepted = Hovering && Seconds(now) >= 3; Leave(); return accepted; }
        public void Leave() { dragging = Hovering = false; entered = 0; }
    }

    // Unity 主线程不承担 OLE apartment；独立 STA 持续泵消息，回调不访问 Unity API。
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class PetFileDropTarget : IDisposable, IFileDropTarget
    {
        private readonly IntPtr hwnd;
        private readonly object gate = new object();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly FileDropHover hover = new FileDropHover();
        private readonly ManualResetEvent ready = new ManualResetEvent(false);
        private readonly Thread thread;
        private string[] pendingFiles;
        private FileDropPoint dropPoint;
        private string previewExtension = "";
        private volatile bool stopped;
        private volatile Exception error;
        private uint threadId;

        public PetFileDropTarget(IntPtr hwnd)
        {
            this.hwnd = hwnd;
            thread = new Thread(Run) { IsBackground = true, Name = "ElecPet 文件拖放" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!ready.WaitOne(3000)) { Dispose(); throw new TimeoutException("文件拖放初始化超时。"); }
            if (error != null) { Dispose(); throw new InvalidOperationException("文件拖放不可用。", error); }
        }

        public bool Hovering { get { lock (gate) return hover.Hovering; } }
        public bool DraggingFiles { get { lock (gate) return hover.DraggingFiles; } }
        public double HoverSeconds { get { lock (gate) return hover.Seconds(clock.Elapsed.TotalSeconds); } }
        public bool Armed { get { return HoverSeconds >= 3; } }
        public string PreviewExtension { get { lock (gate) return previewExtension; } }

        public void SetTarget(bool hit, bool available)
        {
            if (error != null) throw new InvalidOperationException("文件拖放消息线程失败。", error);
            lock (gate)
            {
                // 暂停或业务忙碌会取消待消费投喂；Drop 后移开鼠标不撤回已接受结果。
                if (!available) pendingFiles = null;
                hover.SetTarget(hit, available && !stopped && pendingFiles == null, clock.Elapsed.TotalSeconds);
            }
        }

        public string[] TakeFiles()
        { FileDropPoint point; return TakeFiles(out point); }

        public string[] TakeFiles(out FileDropPoint point)
        {
            lock (gate)
            { point = dropPoint; var files = pendingFiles; pendingFiles = null; return files; }
        }

        int IFileDropTarget.DragEnter(IntPtr data, uint keys, FileDropPoint point, ref uint effect)
        {
            string[] files = ReadFiles(data);
            lock (gate)
            {
                hover.Enter(!stopped && pendingFiles == null && files != null && (effect & 1) != 0, clock.Elapsed.TotalSeconds);
                previewExtension = files == null ? "" : Path.GetExtension(files[0]);
                effect = hover.Hovering ? 1u : 0u;
            }
            return 0;
        }

        int IFileDropTarget.DragOver(uint keys, FileDropPoint point, ref uint effect)
        {
            lock (gate) effect = hover.Hovering && (effect & 1) != 0 ? 1u : 0u;
            return 0;
        }

        int IFileDropTarget.DragLeave()
        {
            lock (gate) { hover.Leave(); previewExtension = ""; }
            return 0;
        }

        int IFileDropTarget.Drop(IntPtr data, uint keys, FileDropPoint point, ref uint effect)
        {
            string[] files = ReadFiles(data);
            lock (gate)
            {
                bool armed = hover.Drop(clock.Elapsed.TotalSeconds);
                bool accepted = armed && !stopped && pendingFiles == null && files != null && (effect & 1) != 0;
                if (accepted) { pendingFiles = files; dropPoint = point; }
                effect = accepted ? 1u : 0u;
                previewExtension = "";
            }
            return 0;
        }

        private static string[] ReadFiles(IntPtr data)
        {
            if (data == IntPtr.Zero) return null;
            var getData = (GetData)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(data), 3 * IntPtr.Size), typeof(GetData));
            IntPtr format = IntPtr.Zero, medium = IntPtr.Zero;
            bool received = false;
            try
            {
                // Mono 对系统 FORMATETC 的结构封送会阻塞跨进程 GetData，按原生 ABI 传入缓冲区。
                int formatSize = IntPtr.Size == 8 ? 32 : 20, mediumSize = IntPtr.Size == 8 ? 24 : 12;
                format = Marshal.AllocHGlobal(formatSize); medium = Marshal.AllocHGlobal(mediumSize);
                for (int offset = 0; offset < formatSize; offset++) Marshal.WriteByte(format, offset, 0);
                for (int offset = 0; offset < mediumSize; offset++) Marshal.WriteByte(medium, offset, 0);
                Marshal.WriteInt16(format, 0, 15);
                Marshal.WriteInt32(format, IntPtr.Size == 8 ? 16 : 8, 1);
                Marshal.WriteInt32(format, IntPtr.Size == 8 ? 20 : 12, -1);
                Marshal.WriteInt32(format, IntPtr.Size == 8 ? 24 : 16, 1);
                received = getData(data, format, medium) >= 0;
                if (!received || Marshal.ReadInt32(medium) != 1) return null;
                IntPtr drop = Marshal.ReadIntPtr(medium, IntPtr.Size);
                if (drop == IntPtr.Zero) return null;
                uint count = DragQueryFile(drop, uint.MaxValue, null, 0);
                var files = new List<string>();
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (uint index = 0; index < count; index++)
                {
                    uint length = DragQueryFile(drop, index, null, 0);
                    var path = new StringBuilder((int)length + 1);
                    DragQueryFile(drop, index, path, (uint)path.Capacity);
                    string file = path.ToString();
                    // 这里只接收现存普通文件路径；虚拟内容和目录不能成为喂食候选。
                    if (!Path.IsPathRooted(file) || !File.Exists(file)) return null;
                    if (unique.Add(file)) files.Add(file);
                }
                return files.Count == 0 ? null : files.ToArray();
            }
            finally
            {
                try { if (received) ReleaseStgMedium(medium); }
                finally
                {
                    if (medium != IntPtr.Zero) Marshal.FreeHGlobal(medium);
                    if (format != IntPtr.Zero) Marshal.FreeHGlobal(format);
                }
            }
        }

        private void Run()
        {
            bool initialized = false, registered = false;
            IntPtr target = IntPtr.Zero;
            try
            {
                threadId = GetCurrentThreadId();
                Message message;
                PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
                Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero)); initialized = true;
                target = Marshal.GetComInterfaceForObject(this, typeof(IFileDropTarget));
                Marshal.ThrowExceptionForHR(RegisterDragDrop(hwnd, target)); registered = true;
                ready.Set();
                while (!stopped)
                {
                    int result = GetMessage(out message, IntPtr.Zero, 0, 0);
                    if (result == 0) break;
                    if (result < 0) throw new Win32Exception();
                    TranslateMessage(ref message); DispatchMessage(ref message);
                }
            }
            catch (Exception exception) { error = exception; }
            finally
            {
                if (registered) RevokeDragDrop(hwnd);
                if (target != IntPtr.Zero) Marshal.Release(target);
                if (initialized) OleUninitialize();
                lock (gate) { hover.Leave(); previewExtension = ""; }
                ready.Set();
            }
        }

        public void Dispose()
        {
            if (stopped) return;
            stopped = true;
            lock (gate) { hover.Leave(); pendingFiles = null; previewExtension = ""; }
            if (threadId != 0) PostThreadMessage(threadId, 0x12, UIntPtr.Zero, IntPtr.Zero);
            if (thread.Join(1000)) ready.Dispose();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetData(IntPtr data, IntPtr format, IntPtr medium);
        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public FileDropPoint Point; public uint Private; }
        [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
        [DllImport("ole32.dll")] private static extern void OleUninitialize();
        [DllImport("ole32.dll")] private static extern int RegisterDragDrop(IntPtr hwnd, IntPtr target);
        [DllImport("ole32.dll")] private static extern int RevokeDragDrop(IntPtr hwnd);
        [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(IntPtr medium);
        [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder path, uint length);
        [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessage(out Message message, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wparam, IntPtr lparam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FileDropPoint { public int X, Y; }

    [ComVisible(true), Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileDropTarget
    {
        [PreserveSig] int DragEnter(IntPtr data, uint keys, FileDropPoint point, ref uint effect);
        [PreserveSig] int DragOver(uint keys, FileDropPoint point, ref uint effect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop(IntPtr data, uint keys, FileDropPoint point, ref uint effect);
    }
}
