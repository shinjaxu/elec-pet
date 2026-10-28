using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ElecPet.Windows
{
    public static class WindowsFileFeeding
    {
        private const int Abort = unchecked((int)0x80004004);
        private const uint RecycleIfPossible = 0x80;

        // 仅接受明确的本机普通文件；不解析快捷方式、不读取内容，也不跟随重解析点。
        public static string ValidateFile(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 3 || !char.IsLetter(path[0])
                || path[1] != ':' || (path[2] != '\\' && path[2] != '/') || path.IndexOf(':', 2) >= 0)
                throw new ArgumentException("只接受本机固定磁盘上的完整文件路径。", "path");
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            if (GetDriveType(root) != 3) throw new NotSupportedException("该磁盘不能保证进入本机回收站。");
            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.Directory) != 0)
                throw new ArgumentException("只能喂食文件，不能喂食文件夹或磁盘根目录。", "path");
            // 硬链接共享只读属性；拒绝此类文件，避免为清理保全链接而更改用户文件属性。
            if ((attributes & FileAttributes.ReadOnly) != 0)
                throw new NotSupportedException("只读文件暂不支持喂食，请先由你自行调整文件属性。");
            // 父目录也不能是链接或挂载点，避免路径表面属于固定盘而实际指向其他位置。
            for (string current = fullPath; current != null; current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new NotSupportedException("不能喂食符号链接或重解析目录中的文件。");
            var info = new RecycleBinInfo { Size = (uint)Marshal.SizeOf(typeof(RecycleBinInfo)) };
            int queryResult = SHQueryRecycleBin(root, ref info);
            if (queryResult < 0) throw new NotSupportedException("无法确认该磁盘的回收站状态。", Marshal.GetExceptionForHR(queryResult));
            return fullPath;
        }

        // 调用者在专用 STA 线程执行；只有 Shell 确认新回收项目后才允许播放满足反馈。
        public static void RecycleFile(string path, IntPtr owner)
        {
            string fullPath = ValidateFile(path);
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("文件回收必须在专用 STA 线程执行。");
            // Shell 在回收站容量不足时仍可能永久删除；同卷硬链接保留数据，不能仅信任回收标志。
            string guard = Path.Combine(Path.GetDirectoryName(fullPath), ".ElecPet-feed-" + Guid.NewGuid().ToString("N") + ".guard");
            if (!CreateHardLink(guard, fullPath, IntPtr.Zero))
                throw new IOException("无法创建文件保全链接，本次喂食已取消。", new Win32Exception());
            bool recycled = false;
            try { RecycleProtectedFile(fullPath, guard, owner); recycled = true; }
            finally
            {
                if (!recycled && !File.Exists(fullPath))
                {
                    try { File.Move(guard, fullPath); }
                    catch (Exception exception)
                    { throw new IOException("无法恢复原路径，文件仍保全在：" + guard, exception); }
                }
                else if (recycled || SameFile(guard, fullPath))
                {
                    try { File.Delete(guard); }
                    catch (Exception exception)
                    { throw new IOException("保全链接未能清理，文件仍保全在：" + guard, exception); }
                }
                else throw new IOException("原路径已被其他文件占用，原文件仍保全在：" + guard);
            }
        }

        private static void RecycleProtectedFile(string fullPath, string guard, IntPtr owner)
        {
            Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 0x6));
            IFileOperation operation = null;
            IShellItem item = null;
            var sink = new RecycleSink(fullPath, guard);
            try
            {
                operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));
                var itemId = typeof(IShellItem).GUID;
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(fullPath, IntPtr.Zero, ref itemId, out item));
                operation.SetOwnerWindow(owner);
                // 请求回收、停止错误并仅处理指定文件，禁止连带 HTML 的同名资源目录。
                // 无确认分支也可能永久移除原路径，由外层保全链接恢复，不能让系统提供永久删除选项。
                operation.SetOperationFlags(0x00080000 | 0x20000000 | 0x00100000 | 0x2000 | 0x0400 | 0x0010 | 0x0004);
                operation.DeleteItem(item, sink);
                operation.PerformOperations();
                bool aborted;
                operation.GetAnyOperationsAborted(out aborted);
                if (aborted || !sink.Recycled)
                    throw new IOException("文件未成功进入回收站，已停止本次喂食。", sink.Error);
            }
            finally
            {
                if (item != null) Marshal.ReleaseComObject(item);
                if (operation != null) Marshal.ReleaseComObject(operation);
                GC.KeepAlive(sink);
                CoUninitialize();
            }
        }

        private static bool SameFile(string first, string second)
        {
            // 只读取文件标识，不读取内容；失败时宁可留下保全链接，也不删除可能的唯一副本。
            using (var firstHandle = CreateFile(first, 0, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero))
            using (var secondHandle = CreateFile(second, 0, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero))
            {
                FileInformation left, right;
                return !firstHandle.IsInvalid && !secondHandle.IsInvalid
                    && GetFileInformationByHandle(firstHandle, out left) && GetFileInformationByHandle(secondHandle, out right)
                    && left.Volume == right.Volume && left.IndexHigh == right.IndexHigh && left.IndexLow == right.IndexLow;
            }
        }

        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        private sealed class RecycleSink : IFileOperationProgressSink
        {
            private readonly string path, guard;
            public bool Recycled { get; private set; }
            public Exception Error { get; private set; }
            public RecycleSink(string path, string guard) { this.path = path; this.guard = guard; }
            public int PreDeleteItem(uint flags, IntPtr item)
            {
                // Shell 已标明不可回收时提前中止；容量等后续回退仍由外层硬链接保全。
                if ((flags & RecycleIfPossible) == 0)
                { Error = new IOException("当前文件不能放入回收站，已拒绝永久删除。"); return Abort; }
                try
                {
                    ValidateFile(path);
                    // 原路径若被其他进程替换，不能回收新文件，更不能清理旧文件的唯一保全链接。
                    if (!SameFile(path, guard)) throw new IOException("原文件已被其他进程替换，已取消回收。");
                    return 0;
                }
                catch (Exception exception) { Error = exception; return Abort; }
            }
            public int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr recycledItem)
            {
                Recycled = result >= 0 && recycledItem != IntPtr.Zero;
                if (!Recycled) Error = result < 0 ? Marshal.GetExceptionForHR(result) : new IOException("Shell 未返回可恢复的回收项目。");
                return Recycled ? 0 : Abort;
            }
            public int StartOperations() { return 0; }
            public int FinishOperations(int result) { if (result < 0) Error = Marshal.GetExceptionForHR(result); return 0; }
            public int PreRenameItem(uint flags, IntPtr item, IntPtr name) { return Abort; }
            public int PostRenameItem(uint flags, IntPtr item, IntPtr name, int result, IntPtr created) { return Abort; }
            public int PreMoveItem(uint flags, IntPtr item, IntPtr folder, IntPtr name) { return Abort; }
            public int PostMoveItem(uint flags, IntPtr item, IntPtr folder, IntPtr name, int result, IntPtr created) { return Abort; }
            public int PreCopyItem(uint flags, IntPtr item, IntPtr folder, IntPtr name) { return Abort; }
            public int PostCopyItem(uint flags, IntPtr item, IntPtr folder, IntPtr name, int result, IntPtr created) { return Abort; }
            public int PreNewItem(uint flags, IntPtr folder, IntPtr name) { return Abort; }
            public int PostNewItem(uint flags, IntPtr folder, IntPtr name, IntPtr template, uint attributes, int result, IntPtr created) { return Abort; }
            public int UpdateProgress(uint total, uint completed) { return 0; }
            public int ResetTimer() { return 0; }
            public int PauseTimer() { return 0; }
            public int ResumeTimer() { return 0; }
        }

        [StructLayout(LayoutKind.Sequential)] private struct RecycleBinInfo
        { public uint Size; public long Bytes, Items; }
        [StructLayout(LayoutKind.Sequential)] private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode)] private static extern uint GetDriveType(string root);
        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string newPath, string existingPath, IntPtr attributes);
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr attributes, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
        [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)] private static extern int SHQueryRecycleBin(string root, ref RecycleBinInfo info);
        [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
        [DllImport("ole32.dll")] private static extern void CoUninitialize();
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem { }
        // 未调用的槽位仍按 Windows SDK 顺序保留，不能省略中间的方法。
        [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOperation
        {
            void Advise(); void Unadvise(); void SetOperationFlags(uint flags); void SetProgressMessage();
            void SetProgressDialog(); void SetProperties(); void SetOwnerWindow(IntPtr owner);
            void ApplyPropertiesToItem(); void ApplyPropertiesToItems(); void RenameItem(); void RenameItems();
            void MoveItem(); void MoveItems(); void CopyItem(); void CopyItems();
            void DeleteItem(IShellItem item, IFileOperationProgressSink sink);
            void DeleteItems(); void NewItem(); void PerformOperations();
            void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
        }
        [ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IFileOperationProgressSink
        {
            [PreserveSig] int StartOperations();
            [PreserveSig] int FinishOperations(int result);
            [PreserveSig] int PreRenameItem(uint flags, IntPtr item, IntPtr name);
            [PreserveSig] int PostRenameItem(uint flags, IntPtr item, IntPtr name, int result, IntPtr created);
            [PreserveSig] int PreMoveItem(uint flags, IntPtr item, IntPtr folder, IntPtr name);
            [PreserveSig] int PostMoveItem(uint flags, IntPtr item, IntPtr folder, IntPtr name, int result, IntPtr created);
            [PreserveSig] int PreCopyItem(uint flags, IntPtr item, IntPtr folder, IntPtr name);
            [PreserveSig] int PostCopyItem(uint flags, IntPtr item, IntPtr folder, IntPtr name, int result, IntPtr created);
            [PreserveSig] int PreDeleteItem(uint flags, IntPtr item);
            [PreserveSig] int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr recycledItem);
            [PreserveSig] int PreNewItem(uint flags, IntPtr folder, IntPtr name);
            [PreserveSig] int PostNewItem(uint flags, IntPtr folder, IntPtr name, IntPtr template, uint attributes, int result, IntPtr created);
            [PreserveSig] int UpdateProgress(uint total, uint completed);
            [PreserveSig] int ResetTimer();
            [PreserveSig] int PauseTimer();
            [PreserveSig] int ResumeTimer();
        }
    }
}
