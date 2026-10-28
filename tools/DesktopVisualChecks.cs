using System;
using System.IO;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using ElecPet.Core;
using ElecPet.Windows;

public static class DesktopVisualChecks
{
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativeWindow.Point point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int size);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [STAThread]
    public static int Main(string[] args)
    {
        string marker = Guid.NewGuid().ToString("N");
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ElecPet.VisualProbe." + marker + ".txt");
        try
        {
            using (var session = DesktopIcons.OpenSession())
            {
                var before = session.Read();
                if (before.AutoArrange) throw new InvalidOperationException("自动排列已开启，停止探测。");
                File.WriteAllText(path, marker);
                object desktopShell = null;
                try
                {
                    DesktopItem fixture = null;
                    for (int i = 0; i < 100 && fixture == null; i++)
                    {
                        fixture = session.Read().Items.Find(item => string.Equals(item.Id, path, StringComparison.OrdinalIgnoreCase));
                        if (fixture == null) Thread.Sleep(50);
                    }
                    if (fixture == null) throw new InvalidOperationException("测试图标未出现。");
                    var condition = new PropertyCondition(AutomationElement.NameProperty, fixture.Name);
                    var viewWindow = new IntPtr(long.Parse(before.ViewId.Split(':')[0]));
                    var element = AutomationElement.FromHandle(viewWindow).FindFirst(TreeScope.Descendants, condition);
                    if (element == null) throw new InvalidOperationException("无法读取测试图标显示区域。");
                    var start = element.Current.BoundingRectangle;
                    Console.WriteLine("Shell origin={0},{1}; UIA bounds={2}; iconSize={3}", fixture.Position.X, fixture.Position.Y, start, before.IconSize);
                    Console.WriteLine("Shell view={0}", before.ViewId);
                    bool pixels = Array.IndexOf(args, "--pixels") >= 0;
                    if (pixels)
                    {
                        desktopShell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                        desktopShell.GetType().InvokeMember("MinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, desktopShell, null);
                        Thread.Sleep(300);
                    }
                    Bitmap baseline = pixels ? Capture(fixture.Position, before.IconSize, "before") : null;
                    // 检查连续搬运的中间帧，而非恢复网格吸附后的最终坐标。
                    foreach (int distance in new[] { 12, 24, 0 })
                    {
                        var target = new Point2(fixture.Position.X + distance, fixture.Position.Y);
                        var actual = session.Move(fixture.Id, target);
                        if (!DesktopOrganization.Near(actual, target)) throw new InvalidOperationException("坐标读回未命中。");
                        Thread.Sleep(50);
                        var shown = element.Current.BoundingRectangle;
                        if (pixels)
                        {
                            using (var frame = Capture(fixture.Position, before.IconSize, "offset-" + distance))
                            {
                                int samples = 0, matched = 0;
                                int originX = (int)fixture.Position.X - Math.Max(0, (int)fixture.Position.X - 22);
                                // 使用文本文件图标中的灰白前景作为模板，排除壁纸和随机文件名的干扰。
                                for (int x = 0; x < before.IconSize; x++)
                                    for (int y = 0; y < before.IconSize; y++)
                                    {
                                        var expected = baseline.GetPixel(originX + x, y);
                                        if (expected.R < 180 || Math.Abs(expected.R - expected.G) > 3 || Math.Abs(expected.R - expected.B) > 3) continue;
                                        samples++;
                                        if (frame.GetPixel(originX + x + distance, y).ToArgb() == expected.ToArgb()) matched++;
                                    }
                                Console.WriteLine("iconPixelsAtRequestedPosition={0}/{1}", matched, samples);
                                if (samples < 50 || matched < samples * .9)
                                    throw new InvalidOperationException("图标像素没有及时显示在请求的中间位置。");
                            }
                        }
                        Console.WriteLine("offset={0}; visibleOffset={1}; readbackOffset={2}", distance, shown.X - start.X, actual.X - fixture.Position.X);
                        if (Math.Abs(shown.X - start.X - distance) > 2)
                            throw new InvalidOperationException("坐标已更新，但图标显示区域仍停在旧位置。");
                    }
                    session.RestoreAlignment();
                    if (baseline != null) baseline.Dispose();
                }
                finally
                {
                    try
                    {
                        try { session.RestoreAlignment(); }
                        finally { if (File.Exists(path) && File.ReadAllText(path) == marker) File.Delete(path); }
                    }
                    finally
                    {
                        // 文件或网格清理失败也必须还原测试前的窗口状态。
                        if (desktopShell != null)
                        {
                            try { desktopShell.GetType().InvokeMember("UndoMinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, desktopShell, null); }
                            finally { Marshal.ReleaseComObject(desktopShell); }
                        }
                    }
                }
                var after = session.Read();
                foreach (var item in before.Items)
                {
                    var current = after.Items.Find(value => value.Id == item.Id);
                    if (current == null || !DesktopOrganization.Near(item.Position, current.Position))
                        throw new InvalidOperationException("已有图标位置发生变化。");
                }
                Console.WriteLine("PASS: intermediate icon display positions follow writes; existing {0} items unchanged", before.Items.Count);
            }
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static Bitmap Capture(Point2 position, int iconSize, string name)
    {
        var at = WindowFromPoint(new NativeWindow.Point { X = (int)position.X + 16, Y = (int)position.Y + 16 });
        var type = new System.Text.StringBuilder(256); GetClassName(at, type, type.Capacity);
        Console.WriteLine("sampleWindow={0}; hwnd={1}; parent={2}", type, at, GetParent(at));
        if (type.ToString() != "SysListView32" && type.ToString() != "SHELLDLL_DefView" && type.ToString() != "WorkerW")
            throw new InvalidOperationException("像素取样区域被其他应用遮挡，本次像素结果无效。");
        var image = new Bitmap(Math.Max(128, iconSize + 48), Math.Max(84, iconSize));
        using (var graphics = Graphics.FromImage(image))
            graphics.CopyFromScreen(Math.Max(0, (int)position.X - 22), (int)position.Y, 0, 0, image.Size);
        image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".png"), ImageFormat.Png);
        return image;
    }
}
