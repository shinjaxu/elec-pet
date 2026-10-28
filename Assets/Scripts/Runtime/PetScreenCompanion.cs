using System;
using System.Threading.Tasks;
using ElecPet.Core;
using ElecPet.Windows;
using UnityEngine;

namespace ElecPet.Runtime
{
    public sealed class PetScreenCompanion : IDisposable
    {
        private readonly IntPtr petWindow;
        private readonly bool captureAllowed;
        private readonly ForegroundWatch foreground;
        private readonly ScreenObservationClock clock = new ScreenObservationClock();
        private ObservedWindow observed;
        private Task<ScreenPixels> capture;
        private LocalOllamaRequest request;
        private bool checking, ready, disposed, manualPending;
        private long generation, replyGeneration;
        private double nextPoll, retryAt, replyUntil;
        public bool Enabled { get; private set; }
        public string Reply { get; private set; }
        public double ReplyExpiresAt { get { return replyUntil; } }
        public string Status { get; private set; }

        public PetScreenCompanion(IntPtr petWindow, bool enabled)
        {
            this.petWindow = petWindow;
            Enabled = enabled;
            captureAllowed = WindowsScreenObserver.ExcludeFromCapture(petWindow);
            if (captureAllowed)
            {
                foreground = new ForegroundWatch();
                captureAllowed = foreground.Active;
            }
            Status = enabled ? "等待观察" : "已关闭";
        }

        // 主线程只编排观察、捕获和回复展示；像素读取与本机网络调用均在后台。
        public void Tick(double now, bool available)
        {
            if (disposed) return;
            if (!Enabled || !captureAllowed || !available)
            {
                Suspend(now);
                Status = !Enabled ? "已关闭" : !captureAllowed ? "系统不支持安全截图" : "暂停观察";
                return;
            }
            try
            {
                // 返回结果时立即核对前台，避免轮询间隔内切换仍显示旧回复。
                if (now >= nextPoll || request != null && request.IsCompleted || capture != null && capture.IsCompleted)
                {
                    var current = WindowsScreenObserver.ReadForeground(petWindow);
                    if (current == null)
                    {
                        Suspend(now);
                        nextPoll = now + .25;
                        Status = "等待有效窗口";
                        return;
                    }
                    UpdateObservedWindow(current, foreground.Revision, now);
                    nextPoll = now + .25;
                }
                if (!clock.IsCurrent(replyGeneration) || now >= replyUntil) Reply = null;
                if (!checking && !clock.IsCurrent(generation)) CancelRequest();
                if (request != null)
                {
                    if (!request.IsCompleted) return;
                    if (request.Cancelled) { request = null; checking = false; }
                    else CompleteRequest(now);
                }
                if (capture != null)
                {
                    if (!capture.IsCompleted) return;
                    var completed = capture;
                    capture = null;
                    ScreenPixels pixels = completed.GetAwaiter().GetResult();
                    if (!clock.IsCurrent(generation) || pixels == null) return;
                    SendPicture(pixels);
                    return;
                }
                if (observed == null) { Status = "等待有效窗口"; return; }
                if (!ready)
                {
                    if (now >= retryAt)
                    {
                        StartRequest("/api/show", OllamaCompanionProtocol.CreateCheck(), true);
                        Status = "检查本地模型";
                    }
                    return;
                }
                if (manualPending || clock.TryBegin(now, out generation))
                {
                    manualPending = false;
                    var expected = observed;
                    capture = Task.Run(() => WindowsScreenObserver.Capture(expected, petWindow));
                    Status = "看看你在忙什么";
                }
            }
            catch (Exception exception)
            {
                Fail(now, "画面或回复处理", 0, exception);
            }
        }

        // 点击只预留当前窗口；下一次 Tick 核对拖拽、喂食等状态后再截图，复用在途的能力检查。
        public bool RequestImmediate(double now)
        {
            if (disposed || !Enabled || !captureAllowed) return false;
            try
            {
                return TryQueueImmediate(WindowsScreenObserver.ReadForeground(petWindow), foreground.Revision, now);
            }
            catch (Exception exception)
            {
                Fail(now, "手动观察", 0, exception);
                return false;
            }
        }

        private bool TryQueueImmediate(ObservedWindow target, long revision, double now)
        {
            if (target == null || manualPending || capture != null || request != null && !checking) return false;
            UpdateObservedWindow(target, revision, now);
            long nextGeneration;
            if (!clock.TryBeginImmediately(now, out nextGeneration)) return false;
            generation = nextGeneration;
            manualPending = true;
            retryAt = now;
            nextPoll = 0;
            Reply = null;
            return true;
        }

        private void UpdateObservedWindow(ObservedWindow current, long revision, double now)
        {
            observed = current;
            // 事件代次保留轮询间隙切走再切回的事实；模型检查期间的旧点击也必须丢弃。
            clock.Observe(current.Identity + ":" + revision, now, true);
            if (manualPending && !clock.IsCurrent(generation)) manualPending = false;
        }

        private void SendPicture(ScreenPixels pixels)
        {
            var texture = new Texture2D(pixels.Width, pixels.Height, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadRawTextureData(pixels.Rgba);
                texture.Apply(false, false);
                StartRequest("/api/chat", OllamaCompanionProtocol.CreateChat(texture.EncodeToJPG(80)), false);
                Status = "想一句悄悄话";
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }

        private void StartRequest(string path, string json, bool modelCheck)
        {
            if (request != null) throw new InvalidOperationException("已有本地模型请求尚未结束。");
            checking = modelCheck;
            request = new LocalOllamaRequest(path, json, modelCheck ? 5000 : 60000);
        }

        private void CompleteRequest(double now)
        {
            bool modelCheck = checking;
            var completed = request;
            request = null;
            checking = false;
            using (completed)
            {
                var result = completed.Result;
                if (!result.Success)
                {
                    Fail(now, modelCheck ? "模型检查" : "生成回复", result.StatusCode, result.Error);
                    return;
                }
                if (modelCheck)
                {
                    ready = OllamaCompanionProtocol.HasVision(result.Body);
                    if (!ready) { Fail(now, "模型不支持视觉", result.StatusCode); return; }
                    Status = "观察中";
                }
                else if (clock.IsCurrent(generation))
                {
                    Reply = OllamaCompanionProtocol.ReadReply(result.Body);
                    replyGeneration = generation;
                    replyUntil = now + 12;
                    Status = "观察中";
                }
            }
        }

        private void Fail(double now, string stage, long responseCode, Exception exception = null)
        {
            CancelRequest();
            ready = false;
            manualPending = false;
            retryAt = now + 180;
            Reply = null;
            Status = "本地模型未就绪，稍后重试";
            if (exception == null)
                Debug.LogWarningFormat("屏幕陪伴暂不可用，阶段={0}，模型={1}，HTTP状态={2}，180秒后重试。",
                    stage, OllamaCompanionProtocol.Model, responseCode);
            else Debug.LogErrorFormat("屏幕陪伴发生非预期故障，阶段={0}。\n{1}", stage, exception);
        }

        public void SetEnabled(bool enabled, double now)
        {
            Enabled = enabled;
            Suspend(now);
            Status = enabled ? "等待观察" : "已关闭";
        }

        public void Suspend(double now)
        {
            clock.Observe(null, now, false);
            observed = null;
            manualPending = false;
            Reply = null;
            ready = false;
            nextPoll = 0;
            CancelRequest();
            // 原生捕获不能强制中断；取走完成结果后才能发起下一次，保持单个在途任务。
            if (capture != null && capture.IsCompleted) { var ignored = capture.Exception; capture = null; }
        }

        private void CancelRequest()
        {
            if (request == null) return;
            request.Dispose();
            // Abort 后仍等待后台任务结束，取消与恢复不能产生重叠请求。
            if (request.IsCompleted) request = null;
            checking = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (foreground != null) foreground.Dispose();
            Suspend(0);
            if (capture != null)
                capture.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
