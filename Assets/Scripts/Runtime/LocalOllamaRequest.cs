using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ElecPet.Runtime
{
    // 仅本机回环 HTTP；不修改 Unity 全局网络策略，不使用代理，也不跟随重定向。
    public sealed class LocalOllamaRequest : IDisposable
    {
        public sealed class Response
        {
            public long StatusCode;
            public string Body;
            public Exception Error;
            public bool Success { get { return Error == null && StatusCode >= 200 && StatusCode < 300; } }
        }

        private readonly HttpWebRequest request;
        private readonly Task<Response> task;
        private int cancelled;
        public bool IsCompleted { get { return task.IsCompleted; } }
        public bool Cancelled { get { return Interlocked.CompareExchange(ref cancelled, 0, 0) != 0; } }
        public Response Result { get { return task.GetAwaiter().GetResult(); } }

        public LocalOllamaRequest(string path, string body, int timeoutMilliseconds)
        {
            if (path != "/api/show" && path != "/api/chat") throw new ArgumentException("仅允许本地模型接口。", "path");
            request = (HttpWebRequest)WebRequest.Create(OllamaCompanionProtocol.Endpoint + path);
            request.Proxy = null;
            request.AllowAutoRedirect = false;
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = timeoutMilliseconds;
            request.ReadWriteTimeout = timeoutMilliseconds;
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bytes.Length;
            task = Task.Run(() => Send(bytes, timeoutMilliseconds));
        }

        private Response Send(byte[] bytes, int timeoutMilliseconds)
        {
            // 同一截止时间覆盖上传、推理和响应读取，避免分阶段等待叠加。
            using (var timeout = new Timer(_ => request.Abort(), null, timeoutMilliseconds, Timeout.Infinite))
            {
                try
                {
                    using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        return new Response { StatusCode = (long)response.StatusCode, Body = reader.ReadToEnd() };
                }
                catch (WebException exception)
                {
                    using (var response = exception.Response as HttpWebResponse)
                        return new Response { StatusCode = response == null ? 0 : (long)response.StatusCode };
                }
                catch (Exception exception) { return new Response { Error = exception }; }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref cancelled, 1) == 0) request.Abort();
        }
    }
}
