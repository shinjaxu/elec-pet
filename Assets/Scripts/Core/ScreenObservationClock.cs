using System;

namespace ElecPet.Core
{
    // 自动观察每个连续停留会话只触发一次；跨会话共享冷却，代次用于丢弃异步返回的过期结果。
    public sealed class ScreenObservationClock
    {
        private string identity;
        private double startedAt;
        private double nextAllowedAt = double.NegativeInfinity;
        private double nextManualAllowedAt = double.NegativeInfinity;
        private bool handled;
        private long generation;

        public void Observe(string identity, double now, bool available)
        {
            if (!available || string.IsNullOrEmpty(identity))
            {
                // 持续不可用只失效一次；恢复相同窗口也必须作为新会话重新计时。
                if (this.identity != null)
                {
                    this.identity = null;
                    generation++;
                }
                return;
            }
            if (string.Equals(this.identity, identity, StringComparison.Ordinal)) return;
            this.identity = identity;
            startedAt = now;
            handled = false;
            generation++;
        }

        public bool TryBegin(double now, out long generation)
        {
            generation = 0;
            if (identity == null || handled || now - startedAt < 30 || now < nextAllowedAt) return false;
            handled = true;
            nextAllowedAt = now + 180;
            generation = this.generation;
            return true;
        }

        public bool TryBeginImmediately(double now, out long generation)
        {
            generation = 0;
            // 手动请求跳过自动等待，但切窗或暂停不能重置短防抖；拒绝时保留已有请求状态。
            if (identity == null || now < nextManualAllowedAt) return false;
            nextManualAllowedAt = now + 2;
            handled = true;
            nextAllowedAt = now + 180;
            // 同一窗口的手动重试也要使先前异步结果失效。
            generation = ++this.generation;
            return true;
        }

        public bool IsCurrent(long generation) { return identity != null && this.generation == generation; }
    }
}
