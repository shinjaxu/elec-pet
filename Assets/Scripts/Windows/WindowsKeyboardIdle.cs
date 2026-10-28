using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using ElecPet.Core;

namespace ElecPet.Windows
{
    // 独立消息线程避免渲染卡顿阻塞键盘通知；回调只更新时间，并始终把事件交还系统。
    public sealed class WindowsKeyboardIdle : IDisposable
    {
        private readonly KeyboardIdleClock clock = new KeyboardIdleClock(Now());
        private readonly ManualResetEvent ready = new ManualResetEvent(false);
        private readonly Thread thread;
        private readonly HookProc callback;
        private volatile bool stopped;
        private volatile Exception error;
        private uint threadId;
        private IntPtr hook;

        public WindowsKeyboardIdle()
        {
            callback = OnKeyboard;
            thread = new Thread(Run) { IsBackground = true, Name = "ElecPet 键盘空闲" };
            thread.Start();
            if (!ready.WaitOne(3000)) { Dispose(); throw new TimeoutException("键盘空闲检测初始化超时。"); }
            if (error != null) { Dispose(); throw new InvalidOperationException("键盘空闲检测不可用。", error); }
        }
        public double Seconds
        {
            get
            {
                if (stopped) throw new ObjectDisposedException("WindowsKeyboardIdle");
                if (error != null) throw new InvalidOperationException("键盘空闲检测失败。", error);
                // 按住不重复的修饰键也算键盘活动，鼠标按钮不在 KeyboardBusy 的键码范围内。
                return NativeWindow.KeyboardBusy() ? 0 : clock.Seconds(Now());
            }
        }
        private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0) clock.Record(unchecked((uint)message.ToInt64()), Now());
            return CallNextHookEx(hook, code, message, data);
        }
        private void Run()
        {
            try
            {
                threadId = GetCurrentThreadId();
                Message message;
                PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
                hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero) throw new Win32Exception();
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
                if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
                ready.Set();
            }
        }
        public void Dispose()
        {
            if (stopped) return;
            stopped = true;
            if (threadId != 0) PostThreadMessage(threadId, 0x12, UIntPtr.Zero, IntPtr.Zero);
            if (thread.Join(1000)) ready.Dispose();
        }
        private static long Now() { return unchecked((long)GetTickCount64()); }
        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public NativeWindow.Point Point; public uint Private; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessage(out Message message, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wparam, IntPtr lparam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
    }
}
