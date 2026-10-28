using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ElecPet.Core;

namespace ElecPet.Windows
{
    public sealed class IconWorkerResult
    {
        public int Generation;
        public long Sequence;
        public DesktopSnapshot Snapshot;
        public Point2 Position;
        public Exception Error;
        public bool Cancelled;
    }

    // Shell 的所有对象只在这一条 STA 线程创建、调用和释放。
    public sealed class DesktopIconWorker : IDisposable
    {
        private sealed class Request
        { public int Generation; public long Sequence; public string Id; public Point2 Position; }
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly ConcurrentQueue<IconWorkerResult> results = new ConcurrentQueue<IconWorkerResult>();
        private readonly Func<IDesktopIconSession> factory;
        private readonly Func<double> idleSeconds;
        private readonly Thread thread;
        private Request pending;
        private int generation;
        private bool stopped, restore;

        public DesktopIconWorker(Func<IDesktopIconSession> factory, Func<double> idleSeconds)
        {
            this.factory = factory; this.idleSeconds = idleSeconds;
            thread = new Thread(Run) { IsBackground = true, Name = "ElecPet 桌面图标" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }
        public void RequestRead(int generation)
        { Enqueue(new Request { Generation = generation, Sequence = -1 }); }
        public void RequestMove(int generation, long sequence, string id, Point2 position)
        { Enqueue(new Request { Generation = generation, Sequence = sequence, Id = id, Position = position }); }
        private void Enqueue(Request request)
        {
            lock (gate)
            {
                if (stopped || request.Generation != generation)
                {
                    results.Enqueue(new IconWorkerResult { Generation = request.Generation, Sequence = request.Sequence, Cancelled = true });
                    return;
                }
                // 队列只有一个待执行槽，慢 Shell 不会积累旧的动画位置。
                pending = request; wake.Set();
            }
        }
        public void Invalidate(int nextGeneration)
        {
            lock (gate)
            { generation = nextGeneration; pending = null; restore = true; wake.Set(); }
        }
        public bool TryTake(out IconWorkerResult result) { return results.TryDequeue(out result); }
        private bool Current(int value)
        { lock (gate) return !stopped && generation == value; }

        private void Run()
        {
            IDesktopIconSession session = null;
            var expected = new Dictionary<string, Point2>(StringComparer.Ordinal);
            try
            {
                while (true)
                {
                    wake.WaitOne(50);
                    Request request; bool needsRestore;
                    lock (gate)
                    {
                        if (stopped) break;
                        needsRestore = restore; restore = false;
                        request = pending; pending = null;
                    }
                    var result = new IconWorkerResult { Generation = request == null ? generation : request.Generation,
                        Sequence = request == null ? -1 : request.Sequence };
                    try
                    {
                        if (session != null && (needsRestore || idleSeconds() < 30)) session.RestoreAlignment();
                        if (request == null) continue;
                        if (!Current(request.Generation)) { result.Cancelled = true; results.Enqueue(result); continue; }
                        if (session == null) session = factory();
                        if (request.Id == null)
                        {
                            result.Snapshot = session.Read(); expected.Clear();
                            foreach (var item in result.Snapshot.Items) expected.Add(item.Id, item.Position);
                        }
                        else
                        {
                            if (idleSeconds() < 30 || !Current(request.Generation))
                            { result.Cancelled = true; session.RestoreAlignment(); results.Enqueue(result); continue; }
                            Point2 previous;
                            if (!expected.TryGetValue(request.Id, out previous) || !DesktopOrganization.Near(previous, session.Position(request.Id)))
                                throw new InvalidOperationException("图标位置发生变化，等待重新规划。");
                            // 执行前复查，取消不依赖主线程是否及时消费响应。
                            if (idleSeconds() < 30 || !Current(request.Generation)) result.Cancelled = true;
                            else
                            {
                                result.Position = session.Move(request.Id, request.Position);
                                if (!DesktopOrganization.Near(result.Position, request.Position))
                                    throw new InvalidOperationException("桌面未接受连续定位，已停止搬运。");
                                expected[request.Id] = result.Position;
                                result.Cancelled = !Current(request.Generation);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        result.Error = exception;
                        try { if (session != null) session.Dispose(); }
                        catch (Exception cleanup) { result.Error = new AggregateException(exception, cleanup); }
                        session = null; expected.Clear();
                    }
                    results.Enqueue(result);
                }
            }
            finally
            {
                try { if (session != null) session.Dispose(); }
                catch (Exception exception) { results.Enqueue(new IconWorkerResult { Error = exception, Sequence = -1 }); }
                wake.Dispose();
            }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (stopped) return;
                stopped = true; pending = null; wake.Set();
            }
            // 不在 Unity 线程无限等待 Explorer；在途调用返回后由原线程完成清理。
            thread.Join(1000);
        }
    }
}
