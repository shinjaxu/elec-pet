using System;
using System.IO;
using System.Threading;
using ElecPet.Core;
using ElecPet.Windows;

namespace ElecPet.Tests
{
    public static class WindowsRuntimeChecks
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                using (var keyboard = new WindowsKeyboardIdle())
                    Console.WriteLine("PASS: keyboard-only hook initialized; idle={0:F2}s", keyboard.Seconds);
                using (var session = DesktopIcons.OpenSession())
                {
                    var snapshot = session.Read();
                    if (snapshot.Items.Count == 0) throw new InvalidOperationException("实机桌面没有可核对的测试项目。");
                    Console.WriteLine("PASS: Unity Mono Shell read; items={0}; autoArrange={1}", snapshot.Items.Count, snapshot.AutoArrange);
                    if (Array.IndexOf(args, "--write-fixture") >= 0) VerifyFixture(session, snapshot);
                }
                return 0;
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        }

        private static void VerifyFixture(DesktopIconSession session, DesktopSnapshot before)
        {
            if (before.AutoArrange) throw new InvalidOperationException("自动排列已开启，不能执行定位探测。");
            string marker = Guid.NewGuid().ToString("N");
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ElecPet.RuntimeProbe." + marker + ".txt");
            bool created = false;
            try
            {
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(file)) { writer.Write(marker); created = true; }
                DesktopItem fixture = null;
                for (int attempt = 0; attempt < 100 && fixture == null; attempt++)
                {
                    fixture = session.Read().Items.Find(item => string.Equals(item.Id, path, StringComparison.OrdinalIgnoreCase));
                    if (fixture == null) Thread.Sleep(50);
                }
                if (fixture == null) throw new InvalidOperationException("Shell未显示自建测试图标。");
                Point2 last = fixture.Position;
                for (int frame = 0; frame <= 60; frame++)
                {
                    if (!DesktopOrganization.Near(session.Position(fixture.Id), last))
                        throw new InvalidOperationException("测试图标已被外部调整，停止写入。");
                    var point = new Point2(fixture.Position.X + Math.Sin(frame * Math.PI / 60) * 8, fixture.Position.Y);
                    last = session.Move(fixture.Id, point);
                    if (!DesktopOrganization.Near(last, point)) throw new InvalidOperationException("Mono真实图标连续定位未命中。");
                    Thread.Sleep(33);
                }
                session.RestoreAlignment();
                if (!DesktopOrganization.Near(session.Position(fixture.Id), fixture.Position))
                    throw new InvalidOperationException("测试图标未正常回到原位。");
                Console.WriteLine("PASS: Unity Mono real Shell movement; 61 positions read back; fixture returned to origin");
            }
            finally
            {
                session.RestoreAlignment();
                // 只删除本轮以独占创建写入且内容仍匹配的测试文件，不清理用户已有桌面项目。
                if (created && File.Exists(path) && File.ReadAllText(path) == marker) File.Delete(path);
            }
            var after = session.Read();
            foreach (var item in before.Items)
            {
                var current = after.Items.Find(value => value.Id == item.Id);
                if (current == null || !DesktopOrganization.Near(current.Position, item.Position))
                    throw new InvalidOperationException("其他桌面项目发生变化，未执行自动恢复覆盖。");
            }
            Console.WriteLine("PASS: existing {0} desktop items unchanged; owned fixture removed", before.Items.Count);
        }
    }
}
