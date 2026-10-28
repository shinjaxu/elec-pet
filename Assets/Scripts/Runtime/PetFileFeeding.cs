using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ElecPet.Core;
using ElecPet.Windows;

namespace ElecPet.Runtime
{
    // 主线程编排动画与结果；回收在独立 STA 线程运行，避免卡住宠物。
    public sealed class PetFileFeeding
    {
        private readonly IntPtr owner;
        private Task<bool> recycling;
        private float noticeRemaining;
        public readonly PetFeeding Flow = new PetFeeding();
        public bool Busy { get { return Flow.Active; } }
        public bool HasPendingRecycle { get { return recycling != null; } }
        public bool ShowNotice { get { return Busy || noticeRemaining > 0; } }
        public string Notice { get; private set; }
        public string Extension { get { return Path.GetExtension(Flow.CurrentPath ?? ""); } }
        public Exception Error { get; private set; }

        public PetFileFeeding(IntPtr owner) { this.owner = owner; }

        public bool BeginFiles(string[] paths)
        {
            if (Busy) return false;
            var files = new List<string>();
            try
            {
                foreach (string path in paths) files.Add(WindowsFileFeeding.ValidateFile(path));
            }
            catch (Exception exception)
            {
                Notice = exception is NotSupportedException ? exception.Message : "文件无法喂食，请选择可回收的本机普通文件";
                noticeRemaining = 5;
                if (!(exception is ArgumentException) && !(exception is NotSupportedException)) Error = exception;
                return false;
            }
            if (!Flow.Begin(files, true)) return false;
            Notice = "闻一闻，是什么好吃的？";
            return true;
        }

        public void Tick(float seconds, bool advance)
        {
            bool wasActive = Flow.Active;
            noticeRemaining = Math.Max(0, noticeRemaining - seconds);
            if (recycling != null && recycling.IsCompleted)
            {
                var completed = recycling;
                recycling = null;
                bool success = false;
                try { success = completed.GetAwaiter().GetResult(); }
                catch (Exception exception) { Fail("这个文件没能移入回收站", exception); }
                Flow.CompleteRecycle(success);
            }
            if (advance) Flow.Tick(seconds);
            if (advance && Flow.Active)
            {
                string path = Flow.TakeRecyclePath();
                if (path != null)
                    recycling = RunSta(() => { WindowsFileFeeding.RecycleFile(path, owner); return true; });
            }
            if (Flow.Active)
            {
                switch (Flow.Phase)
                {
                    case FeedingPhase.Sniffing: Notice = "闻一闻，是什么好吃的？"; break;
                    case FeedingPhase.Biting: Notice = "啊呜！"; break;
                    case FeedingPhase.Chewing: Notice = "嚼嚼……"; break;
                    case FeedingPhase.Recycling: Notice = "正在放入回收站……"; break;
                    case FeedingPhase.Satisfied: Notice = "好吃！文件已放入回收站"; break;
                    case FeedingPhase.Failed: Notice = "没吃下，文件未成功回收"; break;
                }
            }
            if (wasActive && !Flow.Active) ShowSummary();
        }

        public void Cancel()
        {
            bool wasActive = Flow.Active;
            Flow.Cancel();
            if (wasActive && !Flow.Active) ShowSummary();
        }

        public Exception TakeError()
        {
            var error = Error;
            Error = null;
            return error;
        }

        private void ShowSummary()
        {
            Notice = Flow.Succeeded > 0 || Flow.Failed > 0
                ? "已回收 " + Flow.Succeeded + " 个" + (Flow.Failed > 0 ? " · 未成功 " + Flow.Failed + " 个" : "，谢谢投喂！")
                : "喂食已取消，文件没有改动";
            noticeRemaining = 4;
        }

        private void Fail(string notice, Exception exception)
        {
            Notice = notice;
            noticeRemaining = 5;
            Error = exception;
        }

        private static Task<T> RunSta<T>(Func<T> operation)
        {
            var result = new TaskCompletionSource<T>();
            var thread = new Thread(() =>
            {
                try { result.SetResult(operation()); }
                catch (Exception exception) { result.SetException(exception); }
            }) { IsBackground = true, Name = "桌宠文件喂食" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return result.Task;
        }
    }
}
