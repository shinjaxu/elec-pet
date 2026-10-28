using System;
using System.Collections.Generic;
using System.Threading;
using ElecPet.Core;
using ElecPet.Windows;

namespace ElecPet.Tests
{
    public static class IconWorkerChecks
    {
        public static int Run()
        {
            int count = 0;
            double idle = 30;
            var fake = new Session();
            using (var worker = new DesktopIconWorker(() => fake, () => idle))
            {
                worker.RequestRead(0);
                Check(fake.Entered.WaitOne(3000), "读取进入工作线程", ref count);
                for (int i = 1; i <= 100; i++) worker.RequestMove(0, i, "a", new Point2(i, 0));
                fake.Release.Set();
                Await(worker, result => result.Snapshot != null);
                var last = Await(worker, result => result.Sequence == 100);
                Check(last.Error == null && last.Position.X == 100 && fake.Writes == 1, "排队位置合并为最新请求", ref count);
                Check(fake.Owner != Thread.CurrentThread.ManagedThreadId && fake.Apartment == ApartmentState.STA, "会话由专用STA持有", ref count);
                idle = 29.99;
                worker.RequestMove(0, 101, "a", new Point2(200, 0));
                var denied = Await(worker, result => result.Sequence == 101);
                Check(denied.Cancelled && fake.Writes == 1, "未满30秒拒绝写入", ref count);
                idle = 30;
                worker.Invalidate(1);
                worker.RequestMove(0, 102, "a", new Point2(300, 0));
                var stale = Await(worker, result => result.Sequence == 102);
                Check(stale.Cancelled && fake.Writes == 1, "旧代次请求失效", ref count);
                worker.RequestRead(1);
                Await(worker, result => result.Generation == 1 && result.Snapshot != null);
                fake.Current = new Point2(250, 0);
                worker.RequestMove(1, 103, "a", new Point2(300, 0));
                var conflict = Await(worker, result => result.Sequence == 103);
                Check(conflict.Error is InvalidOperationException && fake.Writes == 1, "源位置变化不能覆盖用户操作", ref count);
            }
            Check(fake.Disposed && fake.Restored && fake.DisposedOn == fake.Owner, "释放和设置恢复发生在所属线程", ref count);
            fake = new Session { FailMove = true }; fake.Release.Set();
            using (var worker = new DesktopIconWorker(() => fake, () => 30))
            {
                worker.RequestRead(0); Await(worker, r => r.Snapshot != null);
                worker.RequestMove(0, 1, "a", new Point2(10, 0));
                var failure = Await(worker, r => r.Sequence == 1);
                Check(failure.Error != null && fake.Disposed, "原生失败释放会话且报告故障", ref count);
            }
            fake = new Session { BlockMove = true }; fake.Release.Set();
            using (var worker = new DesktopIconWorker(() => fake, () => 30))
            {
                worker.RequestRead(0); Await(worker, r => r.Snapshot != null);
                worker.RequestMove(0, 1, "a", new Point2(10, 0));
                if (!fake.MoveEntered.WaitOne(3000)) throw new InvalidOperationException("在途定位未开始");
                worker.Invalidate(1); fake.MoveRelease.Set();
                var cancelled = Await(worker, r => r.Sequence == 1);
                Check(cancelled.Cancelled && cancelled.Generation == 0, "在途旧响应不能确认新任务完成", ref count);
                worker.RequestRead(1);
                var fresh = Await(worker, r => r.Generation == 1 && r.Snapshot != null);
                Check(fresh.Snapshot.Items[0].Position.X == 10, "恢复先读回在途调用的最终真实位置", ref count);
            }
            return count;
        }

        private static IconWorkerResult Await(DesktopIconWorker worker, Predicate<IconWorkerResult> predicate)
        {
            var until = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < until)
            {
                IconWorkerResult result;
                while (worker.TryTake(out result)) if (predicate(result)) return result;
                Thread.Sleep(2);
            }
            throw new InvalidOperationException("等待工作线程响应超时");
        }
        private static void Check(bool value, string name, ref int count)
        { if (!value) throw new InvalidOperationException(name); count++; }

        private sealed class Session : IDesktopIconSession
        {
            public readonly ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(false);
            public readonly ManualResetEvent MoveEntered = new ManualResetEvent(false), MoveRelease = new ManualResetEvent(false);
            public Point2 Current = new Point2(0, 0);
            public int Writes, Owner, DisposedOn;
            public ApartmentState Apartment;
            public bool Disposed, Restored, FailMove, BlockMove;
            public DesktopSnapshot Read()
            {
                Owner = Thread.CurrentThread.ManagedThreadId; Apartment = Thread.CurrentThread.GetApartmentState();
                Entered.Set(); if (!Release.WaitOne(3000)) throw new TimeoutException();
                return new DesktopSnapshot(new List<DesktopItem> { new DesktopItem("a", "a", ".png", false, false, Current) },
                    new DesktopRect(0, 0, 1000, 1000), new Point2(0, 0), new Point2(100, 100), false, "fake");
            }
            public Point2 Position(string id) { return Current; }
            public Point2 Move(string id, Point2 point)
            {
                if (FailMove) throw new InvalidOperationException("模拟定位失败");
                if (BlockMove) { MoveEntered.Set(); if (!MoveRelease.WaitOne(3000)) throw new TimeoutException(); }
                Writes++; Current = point; return Current;
            }
            public void RestoreAlignment() { Restored = true; }
            public void Dispose() { RestoreAlignment(); DisposedOn = Thread.CurrentThread.ManagedThreadId; Disposed = true; }
        }
    }
}
