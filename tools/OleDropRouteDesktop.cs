using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ElecPet.Tests
{
    // 两个进程运行在本轮独占的非输入桌面；真实 OLE 路由不再受用户指针移动影响。
    public static class OleDropRouteDesktop
    {
        public static int Main(string[] args)
        {
            string desktopName = "ElecPetOleProbe-" + Guid.NewGuid().ToString("N");
            string ready = args[2], targetLog = args[3], sourceLog = args[4];
            IntPtr desktop = CreateDesktop(desktopName, null, IntPtr.Zero, 0, 0xc3, IntPtr.Zero);
            IntPtr target = IntPtr.Zero, source = IntPtr.Zero;
            try
            {
                if (desktop == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
                string targetCommand = Quote(args[0]);
                if (args.Length > 6 && args[6] != "-")
                    foreach (string prefix in Encoding.UTF8.GetString(Convert.FromBase64String(args[6])).Split('\0'))
                        targetCommand += " " + Quote(prefix);
                if (args[0] != args[1]) targetCommand += " " + Quote(args[1]);
                targetCommand += " target " + Quote(ready) + " " + args[5] + " isolated";
                if (args.Length > 8 && args[8] == "toggle") targetCommand += " toggle";
                target = Launch(targetCommand, desktopName, targetLog);
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (!File.Exists(ready) && DateTime.UtcNow < deadline) Thread.Sleep(20);
                if (!File.Exists(ready)) throw new TimeoutException("隔离桌面目标未就绪。");
                source = Launch(Quote(args[1]) + " source " + Quote(ready) + " isolated", desktopName, sourceLog);
                if (WaitForSingleObject(source, 14000) != 0) throw new TimeoutException("隔离桌面拖动未结束。");
                if (WaitForSingleObject(target, 10000) != 0) throw new TimeoutException("隔离桌面目标未结束。");
                uint result; GetExitCodeProcess(source, out result);
                Console.WriteLine(File.ReadAllText(sourceLog));
                string targetText = File.ReadAllText(targetLog);
                bool printedOver = false;
                foreach (string line in targetText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("EVENT DragOver")) { if (printedOver) continue; printedOver = true; }
                    Console.WriteLine(line);
                }
                Console.WriteLine("ISOLATED SOURCE EXIT " + result);
                bool accepted = result == 0 && targetText.Contains("ACCEPTED");
                if (args.Length > 7 && args[7] == "reject") return result == 2 && !targetText.Contains("ACCEPTED") ? 0 : 2;
                return accepted ? 0 : 2;
            }
            catch (Exception error) { Console.WriteLine(error); return 1; }
            finally
            {
                // 只终止本轮直接创建且仍在运行的诊断进程，绝不枚举或终止用户应用。
                CloseChild(source); CloseChild(target);
                if (desktop != IntPtr.Zero) CloseDesktop(desktop);
            }
        }

        private static IntPtr Launch(string command, string desktop, string log)
        {
            var security = new Security { Size = Marshal.SizeOf(typeof(Security)), Inherit = true };
            IntPtr output = CreateFile(log, 0x40000000, 3, ref security, 2, 0x80, IntPtr.Zero);
            if (output == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception();
            try
            {
                var startup = new Startup { Size = Marshal.SizeOf(typeof(Startup)), Desktop = "winsta0\\" + desktop,
                    Flags = 0x100, Output = output, Error = output };
                ProcessInfo child;
                if (!CreateProcess(null, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, true, 0x08000000,
                    IntPtr.Zero, null, ref startup, out child)) throw new System.ComponentModel.Win32Exception();
                CloseHandle(child.Thread);
                return child.Process;
            }
            finally { CloseHandle(output); }
        }

        private static void CloseChild(IntPtr process)
        {
            if (process == IntPtr.Zero) return;
            if (WaitForSingleObject(process, 0) != 0) { TerminateProcess(process, 1); WaitForSingleObject(process, 3000); }
            CloseHandle(process);
        }
        private static string Quote(string value) { return "\"" + value + "\""; }
        [StructLayout(LayoutKind.Sequential)] private struct Security
        { public int Size; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Startup
        {
            public int Size; public string Reserved, Desktop, Title; public uint X, Y, Width, Height, CharsX, CharsY, Fill, Flags;
            public ushort Show, ReservedSize; public IntPtr ReservedData, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo
        { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDesktop(string name, string device, IntPtr mode, uint flags, uint access, IntPtr security);
        [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(string path, uint access, uint share, ref Security security, uint creation, uint attributes, IntPtr template);
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string app, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
            bool inherit, uint flags, IntPtr environment, string directory, ref Startup startup, out ProcessInfo process);
        [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(IntPtr process, out uint result);
        [DllImport("kernel32.dll")] private static extern bool TerminateProcess(IntPtr process, uint code);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
