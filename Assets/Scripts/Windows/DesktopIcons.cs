using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ElecPet.Core;

namespace ElecPet.Windows
{
    public interface IDesktopIconSession : IDisposable
    {
        DesktopSnapshot Read();
        Point2 Move(string id, Point2 screenPosition);
        Point2 Position(string id);
        void RestoreAlignment();
    }

    public static class DesktopIcons
    {
        public static DesktopIconSession OpenSession() { return new DesktopIconSession(); }
        // 会话读取不改变桌面设置，保留独立的只读探测入口。
        public static List<Point2> ReadPositions()
        {
            using (var session = OpenSession())
            {
                var result = new List<Point2>();
                foreach (var item in session.Read().Items) result.Add(item.Position);
                return result;
            }
        }
    }

    public sealed class DesktopIconSession : IDesktopIconSession
    {
        private object windows, desktop, browser, view;
        private IFolderView2 folder;
        private IntPtr viewWindow;
        private readonly Dictionary<string, IntPtr> items = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        private bool alignmentChanged;
        private uint flagsBefore;
        private const uint SnapToGrid = 0x4;

        internal DesktopIconSession()
        {
            try
            {
                windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
                // Unity Mono 不实现 COM 的反射调用，按原生虚表调用桌面查找接口。
                var location = new Variant { Type = 3, Integer = 0 };
                var root = new Variant();
                int desktopHandle;
                ((IShellWindows)windows).FindWindowSW(ref location, ref root, 8, out desktopHandle, 1, out desktop);
                if (desktop == null) throw new InvalidOperationException("桌面视图不可用。");
                var serviceId = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
                var browserId = typeof(IShellBrowser).GUID;
                Marshal.ThrowExceptionForHR(((IServiceProvider)desktop).QueryService(ref serviceId, ref browserId, out browser));
                ((IShellBrowser)browser).QueryActiveShellView(out view);
                ((IShellView)view).GetWindow(out viewWindow);
                folder = (IFolderView2)view;
            }
            catch { Dispose(); throw; }
        }

        public DesktopSnapshot Read()
        {
            CheckView(); ClearItems();
            var result = new List<DesktopItem>();
            var origin = Origin();
            int count; folder.ItemCount(2, out count);
            for (int i = 0; i < count; i++)
            {
                IntPtr pidl; folder.Item(i, out pidl);
                object itemObject = null;
                try
                {
                    var iid = typeof(IShellItem).GUID;
                    folder.GetItem(i, ref iid, out itemObject);
                    var item = (IShellItem)itemObject;
                    string id = Name(item, 0x80028000);
                    string name = Name(item, 0);
                    uint attributes; item.GetAttributes(0x60010000, out attributes);
                    NativeWindow.Point point; folder.GetItemPosition(pidl, out point);
                    bool fileSystem = (attributes & 0x40000000) != 0;
                    result.Add(new DesktopItem(id, name, fileSystem ? Path.GetExtension(id) : "",
                        fileSystem && (attributes & 0x20000000) != 0, (attributes & 0x10000) != 0,
                        new Point2(point.X + origin.X, point.Y + origin.Y)));
                    items.Add(id, pidl); pidl = IntPtr.Zero;
                }
                finally { if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); Release(itemObject); }
            }
            NativeWindow.Point spacing; folder.GetSpacing(out spacing);
            uint viewMode; int iconSize; folder.GetViewModeAndIconSize(out viewMode, out iconSize);
            int auto = folder.GetAutoArrange(); Marshal.ThrowExceptionForHR(auto);
            var work = NativeWindow.ReadWorkArea();
            uint process; GetWindowThreadProcessId(viewWindow, out process);
            var size = new Point2(spacing.X, spacing.Y);
            return new DesktopSnapshot(result, work, DesktopOrganization.DetectGridOrigin(result, work, size),
                size, auto == 0, viewWindow.ToInt64() + ":" + process, iconSize);
        }

        public Point2 Move(string id, Point2 screenPosition)
        {
            CheckView();
            int auto = folder.GetAutoArrange(); Marshal.ThrowExceptionForHR(auto);
            if (auto == 0) throw new InvalidOperationException("请先关闭桌面的自动排列图标。");
            IntPtr pidl;
            if (!items.TryGetValue(id, out pidl)) throw new InvalidOperationException("图标身份已经失效。");
            // 仅临时释放网格吸附，位掩码限制修改范围，不触碰其他布局设置。
            uint flags; folder.GetCurrentFolderFlags(out flags);
            if (!alignmentChanged && (flags & SnapToGrid) != 0)
            {
                flagsBefore = flags;
                alignmentChanged = true;
                folder.SetCurrentFolderFlags(SnapToGrid, 0);
            }
            var origin = Origin();
            var point = new NativeWindow.Point { X = (int)Math.Round(screenPosition.X - origin.X), Y = (int)Math.Round(screenPosition.Y - origin.Y) };
            // 每个搬运位置独立完成视图更新，避免连续定位一直推迟 Shell 的画面刷新。
            folder.SetRedraw(false);
            try { folder.SelectAndPositionItems(1, new[] { pidl }, new[] { point }, 0x80); }
            finally { folder.SetRedraw(true); }
            NativeWindow.Point actual; folder.GetItemPosition(pidl, out actual);
            return new Point2(actual.X + origin.X, actual.Y + origin.Y);
        }

        public Point2 Position(string id)
        {
            CheckView();
            IntPtr pidl;
            if (!items.TryGetValue(id, out pidl)) throw new InvalidOperationException("图标身份已经失效。");
            NativeWindow.Point point; folder.GetItemPosition(pidl, out point);
            var origin = Origin();
            return new Point2(point.X + origin.X, point.Y + origin.Y);
        }

        public void RestoreAlignment()
        {
            if (!alignmentChanged) return;
            uint current; folder.GetCurrentFolderFlags(out current);
            // 用户已重新打开吸附时不再写回；其他标志由掩码保留。
            if ((current & SnapToGrid) == 0) folder.SetCurrentFolderFlags(SnapToGrid, flagsBefore & SnapToGrid);
            alignmentChanged = false;
        }

        private void CheckView()
        { if (folder == null || !IsWindow(viewWindow)) throw new InvalidOperationException("桌面视图已失效。"); }
        private NativeWindow.Point Origin()
        {
            var origin = new NativeWindow.Point();
            if (!NativeWindow.ClientToScreen(viewWindow, ref origin)) throw new Win32Exception();
            return origin;
        }
        private static string Name(IShellItem item, uint kind)
        {
            IntPtr text; item.GetDisplayName(kind, out text);
            try { return Marshal.PtrToStringUni(text); }
            finally { Marshal.FreeCoTaskMem(text); }
        }
        private void ClearItems()
        { foreach (var pidl in items.Values) Marshal.FreeCoTaskMem(pidl); items.Clear(); }
        public void Dispose()
        {
            try { if (folder != null) RestoreAlignment(); }
            finally
            {
                ClearItems(); folder = null;
                Release(view); Release(browser); Release(desktop); Release(windows);
                view = browser = desktop = windows = null;
            }
        }
        private static void Release(object value)
        { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        { [PreserveSig] int QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result); }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct Variant
        {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public int Integer;
        }
        // IShellWindows 继承 IDispatch，前四个占位是 IDispatch 的方法，后续顺序与 Windows SDK 一致。
        [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellWindows
        {
            void GetTypeInfoCount(); void GetTypeInfo(); void GetIDsOfNames(); void Invoke();
            void Count(); void Item(); void NewEnum(); void Register(); void RegisterPending();
            void Revoke(); void OnNavigate(); void OnActivated();
            void FindWindowSW(ref Variant location, ref Variant root, int windowClass, out int hwnd,
                int options, [MarshalAs(UnmanagedType.Interface)] out object desktop);
        }
        [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellView { void GetWindow(out IntPtr hwnd); }
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser
        {
            void GetWindow(); void ContextSensitiveHelp(); void InsertMenusSB(); void SetMenuSB();
            void RemoveMenusSB(); void SetStatusTextSB(); void EnableModelessSB();
            void TranslateAcceleratorSB(); void BrowseObject(); void GetViewStateStream();
            void GetControlWindow(); void SendControlMsg();
            void QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object view);
        }
        // 未调用的槽位也须按 Windows SDK 顺序保留，不能缩短继承接口的虚表。
        [ComImport, Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFolderView2
        {
            void GetCurrentViewMode(); void SetCurrentViewMode(); void GetFolder();
            void Item(int index, out IntPtr pidl); void ItemCount(uint flags, out int count);
            void Items(); void GetSelectionMarkedItem(); void GetFocusedItem();
            void GetItemPosition(IntPtr pidl, out NativeWindow.Point point);
            void GetSpacing(out NativeWindow.Point point); void GetDefaultSpacing();
            [PreserveSig] int GetAutoArrange();
            void SelectItem();
            void SelectAndPositionItems(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] pidls,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] NativeWindow.Point[] points, uint flags);
            void SetGroupBy(); void GetGroupBy(); void SetViewProperty(); void GetViewProperty();
            void SetTileViewProperties(); void SetExtendedTileViewProperties(); void SetText();
            void SetCurrentFolderFlags(uint mask, uint flags); void GetCurrentFolderFlags(out uint flags);
            void GetSortColumnCount(); void SetSortColumns(); void GetSortColumns();
            void GetItem(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
            void GetVisibleItem(); void GetSelectedItem(); void GetSelection(); void GetSelectionState();
            void InvokeVerbOnSelection(); void SetViewModeAndIconSize();
            void GetViewModeAndIconSize(out uint viewMode, out int iconSize);
            void SetGroupSubsetCount(); void GetGroupSubsetCount();
            void SetRedraw([MarshalAs(UnmanagedType.Bool)] bool enabled);
        }
        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(); void GetParent(); void GetDisplayName(uint kind, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
        }
    }
}
