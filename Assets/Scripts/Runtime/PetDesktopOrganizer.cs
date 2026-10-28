using System;
using System.Collections.Generic;
using ElecPet.Core;
using ElecPet.Windows;

namespace ElecPet.Runtime
{
    public sealed class PetDesktopOrganizer : IDisposable
    {
        private readonly DesktopIconWorker worker;
        private readonly DesktopCarry carry = new DesktopCarry();
        private DesktopRect petBounds;
        private List<DesktopMove> moves;
        private int index, generation;
        private long sequence, awaiting;
        private double clock, nextRead;
        private bool reading, verifyNext, suspended, needsPlan;
        private CarryPhase verifiedPhase;
        private Point2 lastPet;
        private Point2 gripOffset = new Point2(0, -28);
        private Exception error;
        public DesktopSnapshot Latest { get; private set; }
        public bool Active { get { return moves != null && index < moves.Count; } }
        public Point2 PetPosition { get { return carry.Phase == CarryPhase.Idle ? lastPet : carry.PetPosition; } }
        public Point2 LookTarget { get { return Active ? (carry.Phase <= CarryPhase.PickUp ? moves[index].From : moves[index].To) : lastPet; } }
        public CarryPhase Phase { get { return carry.Phase; } }
        public double Grip { get { return carry.Grip; } }
        public double Bend { get { return carry.Bend; } }
        public bool Suspended { get { return suspended; } }
        public string Status { get; private set; }
        public int CompletedBatches { get; private set; }
        public int CompletedMoves { get; private set; }

        public PetDesktopOrganizer(DesktopIconWorker worker, DesktopRect workArea)
        { this.worker = worker; Resize(workArea); Status = "等待键盘空闲"; }

        // 编排入口：收取原生结果、判断空闲与有效性，再推进一帧搬运；完成以真实读回为准。
        public void Tick(double dt, double idleSeconds, bool enabled, Point2 petPosition)
        {
            clock += Math.Max(0, dt);
            if (!Active) lastPet = petPosition;
            Receive();
            bool runnable = enabled && idleSeconds >= 30;
            if (Active && !runnable)
            {
                if (!suspended)
                {
                    Invalidate(); suspended = true; verifyNext = true;
                }
                Status = "整理已暂停，等待键盘空闲";
                return;
            }
            if (Active)
            {
                if (suspended) { suspended = false; verifyNext = true; }
                if (verifyNext)
                {
                    if (!reading) { worker.RequestRead(generation); reading = true; }
                    return;
                }
                if (reading || awaiting != 0) return;
                if (carry.Phase == CarryPhase.Idle)
                {
                    carry.Begin(moves[index], petPosition, petBounds, gripOffset);
                    verifiedPhase = CarryPhase.Approach;
                }
                if (carry.Phase == CarryPhase.Complete) { FinishMove(); return; }
                carry.Tick(Math.Min(dt, .1), idleSeconds, true);
                lastPet = carry.PetPosition;
                Status = PhaseStatus(carry.Phase);
                if (carry.WantsWrite)
                {
                    // 走近和搬运可能持续数秒，抓取及放下之前重新核对占位，避免后台新增项目后叠放。
                    if ((carry.Phase == CarryPhase.PickUp || carry.Phase == CarryPhase.PutDown) && verifiedPhase != carry.Phase)
                    { verifyNext = true; return; }
                    awaiting = ++sequence;
                    worker.RequestMove(generation, awaiting, moves[index].Id, carry.IconPosition);
                }
                return;
            }
            if (!reading && clock >= nextRead)
            { reading = true; worker.RequestRead(generation); }
            if (!runnable || reading || Latest == null || !needsPlan) return;
            needsPlan = false;
            var plan = DesktopOrganization.Plan(Latest);
            if (plan.BlockedReason != null) { Status = plan.BlockedReason; return; }
            if (plan.Moves.Count == 0) return;
            moves = plan.Moves; index = 0; verifyNext = true; lastPet = petPosition;
            Status = "准备整理桌面";
        }

        private void Receive()
        {
            IconWorkerResult result;
            while (worker.TryTake(out result))
            {
                if (result.Generation != generation) continue;
                if (result.Error != null)
                {
                    error = result.Error;
                    Abandon(); nextRead = clock + 10;
                    Status = result.Error is InvalidOperationException ? result.Error.Message : "整理暂不可用，稍后再试";
                    continue;
                }
                if (result.Cancelled)
                {
                    if (Active) { Invalidate(); suspended = true; verifyNext = true; }
                    else reading = false;
                    continue;
                }
                if (result.Snapshot != null)
                {
                    reading = false; nextRead = clock + 10;
                    if (SameGeometry(Latest, result.Snapshot))
                    {
                        // 搬运中的图标不是新的网格基准；单图标桌面也须保留开始时的目标网格。
                        var raw = result.Snapshot;
                        result.Snapshot = new DesktopSnapshot(raw.Items, raw.WorkArea, Latest.GridOrigin,
                            raw.Spacing, raw.AutoArrange, raw.ViewId, raw.IconSize);
                    }
                    // 恢复或抓取前核对整份快照，保护用户调整和其他程序新增的桌面项目。
                    if (Active && !SameLayout(Latest, result.Snapshot)) Abandon();
                    Latest = result.Snapshot;
                    if (Active)
                    {
                        if (carry.Phase != CarryPhase.Idle)
                        {
                            var item = Latest.Items.Find(value => value.Id == moves[index].Id);
                            carry.Rebase(lastPet, item.Position);
                        }
                        verifyNext = false;
                        verifiedPhase = carry.Phase;
                    }
                    else needsPlan = true;
                }
                else if (Active && result.Sequence == awaiting)
                {
                    awaiting = 0;
                    UpdateKnownPosition(moves[index].Id, result.Position);
                }
            }
        }

        private void UpdateKnownPosition(string id, Point2 position)
        {
            var items = new List<DesktopItem>(Latest.Items);
            int at = items.FindIndex(item => item.Id == id);
            var old = items[at];
            items[at] = new DesktopItem(old.Id, old.Name, old.Extension, old.Folder, old.Shortcut, position);
            Latest = new DesktopSnapshot(items, Latest.WorkArea, Latest.GridOrigin, Latest.Spacing, Latest.AutoArrange, Latest.ViewId, Latest.IconSize);
        }
        private void FinishMove()
        {
            index++; carry.Cancel();
            if (Active) { verifyNext = true; return; }
            CompletedMoves = index; CompletedBatches++;
            moves = null; needsPlan = false; Invalidate();
            nextRead = clock + 10; Status = "桌面整理完成";
        }
        private void Invalidate()
        { worker.Invalidate(++generation); awaiting = 0; reading = false; }
        private void Abandon()
        { Invalidate(); moves = null; carry.Cancel(); suspended = false; needsPlan = false; }
        public void Interrupt()
        {
            if (!Active) return;
            Abandon(); nextRead = clock; Status = "等待互动结束后再整理";
        }
        public void Resize(DesktopRect area)
        {
            Abandon(); Latest = null; nextRead = clock;
            double mx = Math.Min(88, area.Width / 2), my = Math.Min(88, area.Height / 2);
            petBounds = new DesktopRect(area.X + mx, area.Y + my, area.Width - 2 * mx, area.Height - 2 * my);
        }
        public Exception TakeError() { var value = error; error = null; return value; }
        public void SetGripOffset(Point2 offset) { gripOffset = offset; carry.SetGripOffset(offset); }
        public void Dispose() { worker.Dispose(); }

        private static bool SameLayout(DesktopSnapshot a, DesktopSnapshot b)
        {
            if (!SameGeometry(a, b) || a.AutoArrange != b.AutoArrange || a.Items.Count != b.Items.Count
                || a.GridOrigin.X != b.GridOrigin.X || a.GridOrigin.Y != b.GridOrigin.Y) return false;
            var previous = new Dictionary<string, DesktopItem>(StringComparer.Ordinal);
            foreach (var item in a.Items) previous.Add(item.Id, item);
            foreach (var item in b.Items)
            {
                DesktopItem old;
                if (!previous.TryGetValue(item.Id, out old) || old.Name != item.Name || old.Folder != item.Folder || old.Shortcut != item.Shortcut
                    || old.Extension != item.Extension || !DesktopOrganization.Near(old.Position, item.Position)) return false;
            }
            return true;
        }
        private static bool SameGeometry(DesktopSnapshot a, DesktopSnapshot b)
        {
            return a != null && a.ViewId == b.ViewId && a.IconSize == b.IconSize && a.Spacing.X == b.Spacing.X && a.Spacing.Y == b.Spacing.Y
                && a.WorkArea.X == b.WorkArea.X && a.WorkArea.Y == b.WorkArea.Y && a.WorkArea.Width == b.WorkArea.Width && a.WorkArea.Height == b.WorkArea.Height;
        }
        private static string PhaseStatus(CarryPhase phase)
        {
            switch (phase)
            {
                case CarryPhase.Approach: return "走近需要整理的图标";
                case CarryPhase.PickUp: return "低头叼起图标";
                case CarryPhase.Transport: return "正在搬运图标";
                default: return "轻轻放下图标";
            }
        }
    }
}
