using System;
using ElecPet.Core;

namespace ElecPet.Tests
{
    public static class FeedingChecks
    {
        public static int Run()
        {
            int count = 0;
            var feeding = new PetFeeding();
            Check(!feeding.Active && feeding.Progress < 0, "空闲时不播放喂食", ref count);
            Check(!feeding.Begin(new string[0]), "取消选择不会开始喂食", ref count);
            Check(feeding.Begin(new[] { "a.png", "a.png", "b.mp4" }), "接受一批文件", ref count);
            Check(feeding.CurrentPath == "a.png" && feeding.Phase == FeedingPhase.Sniffing, "先嗅闻首个文件", ref count);
            Check(!feeding.Begin(new[] { "c.txt" }), "不覆盖进行中的批次", ref count);
            Check(feeding.TakeRecyclePath() == null, "咀嚼前不能回收", ref count);
            feeding.Tick(.9);
            Check(feeding.Phase == FeedingPhase.Biting, "嗅闻后叼食", ref count);
            feeding.Tick(.7);
            Check(feeding.Phase == FeedingPhase.Chewing, "叼食后咀嚼", ref count);
            feeding.Tick(30);
            Check(feeding.Phase == FeedingPhase.Recycling && feeding.Progress <= .67, "等待回收时不会跳过成功确认", ref count);
            Check(feeding.TakeRecyclePath() == "a.png", "每次只提交当前文件", ref count);
            Check(feeding.TakeRecyclePath() == null, "每个文件只回收一次", ref count);
            feeding.Tick(30);
            Check(feeding.Phase == FeedingPhase.Recycling && feeding.Succeeded == 0, "未收到结果不能宣告成功", ref count);
            feeding.CompleteRecycle(true);
            Check(feeding.Phase == FeedingPhase.Satisfied && feeding.Succeeded == 1, "回收成功才满足", ref count);
            feeding.CompleteRecycle(true);
            Check(feeding.Succeeded == 1, "重复结果不重复计数", ref count);
            feeding.Tick(2);
            Check(feeding.CurrentPath == "b.mp4" && feeding.Phase == FeedingPhase.Sniffing, "重复路径只吃一次并逐个处理", ref count);
            feeding.Tick(4);
            Check(feeding.TakeRecyclePath() == "b.mp4", "下一个文件独立回收", ref count);
            feeding.CompleteRecycle(false);
            Check(feeding.Phase == FeedingPhase.Failed && feeding.Failed == 1 && feeding.Succeeded == 1, "失败不当成功", ref count);
            feeding.Tick(2);
            Check(!feeding.Active && feeding.Progress < 0, "批次结束恢复空闲", ref count);

            feeding.Begin(new[] { "cancel.txt", "queued.txt" });
            feeding.Tick(1);
            feeding.Cancel();
            Check(!feeding.Active && feeding.TakeRecyclePath() == null, "回收前打断不改文件", ref count);
            feeding.Begin(new[] { "pending.txt", "queued.txt" });
            feeding.Tick(4);
            feeding.Cancel();
            Check(!feeding.Active && feeding.TakeRecyclePath() == null, "尚未提交的回收可以取消", ref count);
            feeding.Begin(new[] { "inflight.txt", "queued.txt" });
            feeding.Tick(4);
            Check(feeding.TakeRecyclePath() == "inflight.txt", "提交真实操作", ref count);
            feeding.Cancel();
            Check(feeding.Active, "已提交操作需等待结果不能假装撤回", ref count);
            Check(feeding.Progress < 0, "取消后保留后台操作，但不再覆盖提起或其他互动姿态", ref count);
            feeding.CompleteRecycle(true);
            Check(!feeding.Active && feeding.Succeeded == 1, "取消保留已回收结果并清空队列", ref count);
            Check(feeding.CurrentPath == null, "取消后没有遗留文件", ref count);
            feeding.Begin(new[] { "again.txt" });
            feeding.Tick(-2);
            Check(feeding.Progress == 0 && feeding.Succeeded == 0 && feeding.Failed == 0, "新批次重置结果且忽略负时间", ref count);
            feeding.CompleteRecycle(true);
            Check(feeding.Phase == FeedingPhase.Sniffing && feeding.Succeeded == 0, "未提交时拒绝外来结果", ref count);
            feeding.Cancel();
            feeding.Begin(new[] { "ready.png" }, true);
            Check(feeding.Phase == FeedingPhase.Biting && Math.Abs(feeding.Progress - .25) < .0001,
                "已悬停准备的首个文件从张嘴姿态连续开始", ref count);
            Check(feeding.TakeRecyclePath() == null, "松手后仍需咀嚼才回收", ref count);
            var behavior = new PetBehavior(new Random(7));
            Check(behavior.TryHunt(140), "准备原有扑跃行为", ref count);
            behavior.AttendFood();
            Check(behavior.State == PetState.Wander, "文件悬停打断旧动作，吃完不能续播旧扑跃", ref count);
            Check(!behavior.TryHunt(140), "喂食不重置原有扑跃冷却", ref count);
            return count;
        }

        private static void Check(bool value, string message, ref int count)
        { if (!value) throw new InvalidOperationException(message); count++; }
    }
}
