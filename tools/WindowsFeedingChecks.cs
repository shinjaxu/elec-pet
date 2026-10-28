using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace ElecPet.Tests
{
    public static class WindowsFeedingChecks
    {
        private static Type service;
        private static int checks;
        private static int dialogObserved;

        [STAThread]
        public static int Main(string[] args)
        {
            string folder = Path.Combine(Path.GetFullPath(args[0]), "fixture-" + Guid.NewGuid().ToString("N"));
            string marker = Guid.NewGuid().ToString("N");
            string file = Path.Combine(folder, "中文 喂食.html");
            string linkedFolder = Path.Combine(folder, "中文 喂食_files");
            string linkedFile = Path.Combine(linkedFolder, "保留.txt");
            string link = Path.Combine(folder, "链接.txt");
            string directoryLink = Path.Combine(folder, "目录链接");
            string oversized = Path.Combine(folder, "oversized-sparse.dat");
            bool ownsFile = false, ownsLinkedFile = false, ownsLink = false, ownsDirectoryLink = false, ownsOversized = false;
            long oversizedLength = 0;
            try
            {
                service = Assembly.GetExecutingAssembly().GetType("ElecPet.Windows.WindowsFileFeeding");
                Check(service != null, "RED: WindowsFileFeeding 安全验证服务尚未实现。");
                Directory.CreateDirectory(folder);
                using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream)) { writer.Write(marker); ownsFile = true; }
                Directory.CreateDirectory(linkedFolder);
                using (var stream = new FileStream(linkedFile, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream)) { writer.Write(marker); ownsLinkedFile = true; }

                Reject(null); Reject(""); Reject("relative.txt"); Reject("C:relative.txt");
                Reject(@"\\server\share\file.txt"); Reject(@"\\?\C:\file.txt");
                Reject(@"\\.\C:\file.txt"); Reject(Path.GetPathRoot(folder));
                Reject(folder); Reject(file + ":stream"); Reject(Path.Combine(folder, "missing.txt"));
                Check((string)Call("ValidateFile", file) == file, "本机固定盘的普通中文文件应允许喂食。");
                Check(File.ReadAllText(file) == marker, "验证路径不能修改文件内容。");
                File.SetAttributes(file, FileAttributes.ReadOnly);
                try { Reject(file); }
                finally { File.SetAttributes(file, FileAttributes.Normal); }

                ownsLink = CreateSymbolicLink(link, file, 2);
                if (ownsLink) Reject(link);
                else Console.WriteLine("SKIP: 本机未允许创建文件符号链接，未执行该实机分支；错误码={0}", Marshal.GetLastWin32Error());
                ownsDirectoryLink = CreateSymbolicLink(directoryLink, linkedFolder, 3);
                if (ownsDirectoryLink) Reject(Path.Combine(directoryLink, "保留.txt"));
                else Console.WriteLine("SKIP: 本机未允许创建目录符号链接，未执行该实机分支；错误码={0}", Marshal.GetLastWin32Error());

                var sinkType = service.GetNestedType("RecycleSink", BindingFlags.NonPublic);
                var sink = Activator.CreateInstance(sinkType, BindingFlags.Public | BindingFlags.Instance, null, new object[] { file, file }, null);
                int rejectedDelete = (int)sinkType.GetMethod("PreDeleteItem").Invoke(sink, new object[] { (uint)0, IntPtr.Zero });
                Check(rejectedDelete < 0 && File.Exists(file), "缺少回收标志时必须在实际删除前中止。");
                int missingReceipt = (int)sinkType.GetMethod("PostDeleteItem").Invoke(sink, new object[] { (uint)0x80, IntPtr.Zero, 0, IntPtr.Zero });
                Check(missingReceipt < 0 && !(bool)sinkType.GetProperty("Recycled").GetValue(sink, null),
                    "Shell 没有返回回收项目时不能误报喂食成功。");
                var replacedSink = Activator.CreateInstance(sinkType, BindingFlags.Public | BindingFlags.Instance, null, new object[] { file, linkedFile }, null);
                int replacedDelete = (int)sinkType.GetMethod("PreDeleteItem").Invoke(replacedSink, new object[] { (uint)0x80, IntPtr.Zero });
                Check(replacedDelete < 0 && File.ReadAllText(file) == marker && File.ReadAllText(linkedFile) == marker,
                    "原路径与保全文件身份不一致时必须取消回收，不能只比较内容或路径。");

                if (Array.IndexOf(args, "--recycle-fixture") >= 0)
                {
                    int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                    using (var watchdog = new Timer(state => CancelOwnDialogs(processId), null, 1000, 100))
                    {
                        Check(ownsFile && File.ReadAllText(file) == marker, "只允许回收本轮独占创建且内容匹配的 fixture。");
                        Call("RecycleFile", file, IntPtr.Zero);
                        Check(!File.Exists(file), "回收成功后原路径仍存在。");
                        Check(File.Exists(linkedFile) && File.ReadAllText(linkedFile) == marker,
                            "回收 HTML 不能连带处理同名资源目录。");
                        Console.WriteLine("PASS: 真实 Shell 回收成功，回调确认回收项目，HTML 资源目录仍保留。");

                        // 稀疏文件仅占少量实际空间，逻辑大小超过整盘容量，验证回收失败不会降级为永久删除。
                        using (var stream = new FileStream(oversized, FileMode.CreateNew, FileAccess.ReadWrite))
                        {
                            ownsOversized = true;
                            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(marker);
                            stream.Write(bytes, 0, bytes.Length);
                            uint returned;
                            if (!DeviceIoControl(stream.SafeFileHandle.DangerousGetHandle(), 0x000900C4, IntPtr.Zero, 0, IntPtr.Zero, 0, out returned, IntPtr.Zero))
                                throw new System.ComponentModel.Win32Exception();
                            oversizedLength = new DriveInfo(Path.GetPathRoot(folder)).TotalSize + 1;
                            stream.SetLength(oversizedLength);
                        }
                        bool oversizedRejected = false;
                        using (var restored = new ManualResetEvent(false))
                        using (var watcher = new FileSystemWatcher(folder))
                        {
                            watcher.NotifyFilter = NotifyFilters.FileName;
                            watcher.Renamed += (sender, change) =>
                            {
                                if (change.Name == "oversized-sparse.dat" && change.OldName.StartsWith(".ElecPet-feed-", StringComparison.Ordinal)) restored.Set();
                            };
                            watcher.EnableRaisingEvents = true;
                            try { Call("RecycleFile", oversized, IntPtr.Zero); }
                            catch (Exception) { oversizedRejected = true; }
                            // Unity Mono 的目录观察器不提供本次重命名事件；原文件和内容校验仍在两种运行时执行。
                            if (Type.GetType("Mono.Runtime") == null)
                                Check(restored.WaitOne(2000), "超大文件必须实际走过 Shell 回退后的保全链接原子恢复。");
                        }
                        Check(oversizedRejected && File.Exists(oversized) && new FileInfo(oversized).Length == oversizedLength && OwnsPrefix(oversized, marker),
                            "不能回收的超大文件必须保留原文件，禁止静默永久删除。");
                        Check(Directory.GetFiles(folder, ".ElecPet-feed-*.guard").Length == 0, "正常完成或恢复后不能遗留保全链接。");
                        Check(Interlocked.CompareExchange(ref dialogObserved, 0, 0) == 0,
                            "系统弹出确认对话框，测试已只取消该对话框，没有选择永久删除。");
                        using (var stream = File.OpenRead(oversized))
                        {
                            stream.Position = oversizedLength / 2;
                            Check(stream.ReadByte() == 0, "恢复后的稀疏文件中间零字节必须保持。");
                            stream.Position = oversizedLength - 1;
                            Check(stream.ReadByte() == 0, "恢复后的稀疏文件末尾零字节必须保持。");
                        }
                        Console.WriteLine("PASS: 超大稀疏文件失败后原路径存在；已写字节、逻辑长度与稀疏零区保持，无对话框或残留链接。");
                    }
                }
                Console.WriteLine("PASS: {0} Windows 文件喂食检查", checks);
                return 0;
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
            finally
            {
                // 清理范围仅限本轮独占创建且内容仍匹配的文件，不遍历或递归删除目录。
                if (ownsLink && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0) File.Delete(link);
                if (ownsDirectoryLink && (File.GetAttributes(directoryLink) & FileAttributes.ReparsePoint) != 0) Directory.Delete(directoryLink);
                if (ownsOversized && File.Exists(oversized) && OwnsPrefix(oversized, marker)) File.Delete(oversized);
                if (ownsFile && File.Exists(file) && File.ReadAllText(file) == marker) File.Delete(file);
                if (ownsLinkedFile && File.Exists(linkedFile) && File.ReadAllText(linkedFile) == marker) File.Delete(linkedFile);
                if (Directory.Exists(linkedFolder) && Directory.GetFileSystemEntries(linkedFolder).Length == 0) Directory.Delete(linkedFolder);
                if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder);
            }
        }

        private static object Call(string name, params object[] args)
        {
            var method = service.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Check(method != null, "缺少文件服务接口：" + name);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }
        private static void Reject(string path)
        {
            bool rejected = false;
            try { Call("ValidateFile", path); }
            catch (ArgumentException) { rejected = true; }
            catch (IOException) { rejected = true; }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected, "不安全路径必须被拒绝：" + (path ?? "<null>"));
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        private static bool OwnsPrefix(string path, string marker)
        {
            using (var reader = new StreamReader(path))
            {
                char[] prefix = new char[marker.Length];
                return reader.ReadBlock(prefix, 0, prefix.Length) == prefix.Length && new string(prefix) == marker;
            }
        }
        private static void CancelOwnDialogs(int processId)
        {
            EnumWindows((window, parameter) =>
            {
                uint windowProcess;
                GetWindowThreadProcessId(window, out windowProcess);
                var className = new System.Text.StringBuilder(32);
                GetClassName(window, className, className.Capacity);
                if (windowProcess == processId && className.ToString() == "#32770")
                {
                    Interlocked.Exchange(ref dialogObserved, 1);
                    PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)] private static extern bool CreateSymbolicLink(string link, string target, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeviceIoControl(IntPtr file, uint code, IntPtr input, uint inputSize,
            IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
        private delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int size);
        [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
