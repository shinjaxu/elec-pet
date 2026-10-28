using System;
using System.Collections.Generic;

namespace ElecPet.Core
{
    public enum FeedingPhase { None, Sniffing, Biting, Chewing, Recycling, Satisfied, Failed }

    // 喂食只编排活动时间；回收操作由外部执行，收到明确结果后才播放吞咽与满足反馈。
    public sealed class PetFeeding
    {
        private readonly Queue<string> pending = new Queue<string>();
        private double elapsed;
        private bool submitted, cancelled;
        public FeedingPhase Phase { get; private set; }
        public string CurrentPath { get; private set; }
        public int Succeeded { get; private set; }
        public int Failed { get; private set; }
        public bool Active { get { return Phase != FeedingPhase.None; } }
        public double Progress
        {
            get
            {
                if (!Active || cancelled) return -1;
                if (Phase == FeedingPhase.Recycling) return .67;
                if (Phase == FeedingPhase.Satisfied || Phase == FeedingPhase.Failed)
                    return .67 + Math.Min(1, elapsed / 1.6) * .33;
                return Math.Min(.67, elapsed / 3.2 * .67);
            }
        }

        public bool Begin(IEnumerable<string> paths, bool alreadySniffed = false)
        {
            if (Active) return false;
            pending.Clear();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
                if (!string.IsNullOrEmpty(path) && unique.Add(path)) pending.Enqueue(path);
            if (pending.Count == 0) return false;
            Succeeded = Failed = 0;
            cancelled = false;
            Next();
            // 首个文件已在三秒悬停中完成准备，松手后从相同张嘴姿态衔接，后续文件仍完整嗅闻。
            if (alreadySniffed) { elapsed = 3.2 * .25 / .67; Phase = FeedingPhase.Biting; }
            return true;
        }

        public void Tick(double seconds)
        {
            if (!Active || seconds <= 0 || Phase == FeedingPhase.Recycling) return;
            elapsed += seconds;
            if (Phase == FeedingPhase.Satisfied || Phase == FeedingPhase.Failed)
            {
                if (elapsed >= 1.6) Next();
                return;
            }
            Phase = elapsed < .85 ? FeedingPhase.Sniffing : elapsed < 1.55 ? FeedingPhase.Biting
                : elapsed < 3.2 ? FeedingPhase.Chewing : FeedingPhase.Recycling;
        }

        public string TakeRecyclePath()
        {
            if (Phase != FeedingPhase.Recycling || submitted) return null;
            submitted = true;
            return CurrentPath;
        }

        public void CompleteRecycle(bool success)
        {
            if (Phase != FeedingPhase.Recycling || !submitted) return;
            if (success) Succeeded++; else Failed++;
            elapsed = 0;
            Phase = success ? FeedingPhase.Satisfied : FeedingPhase.Failed;
            if (cancelled) Next();
        }

        public void Cancel()
        {
            pending.Clear();
            cancelled = true;
            // 已交给系统的文件操作不能撤回；保留会话直到结果返回，防止误报“未改文件”。
            if (Phase != FeedingPhase.Recycling || !submitted) Next();
        }

        private void Next()
        {
            elapsed = 0;
            submitted = false;
            CurrentPath = pending.Count > 0 ? pending.Dequeue() : null;
            Phase = CurrentPath == null ? FeedingPhase.None : FeedingPhase.Sniffing;
        }
    }
}
