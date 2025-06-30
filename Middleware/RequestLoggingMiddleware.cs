using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScanerServer.Data;
using ScanerServer.Models;

namespace ScanerServer.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Action<ScanerServer.Models.HttpRequest> _onRequestReceived;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            Action<ScanerServer.Models.HttpRequest> onRequestReceived
        )
        {
            _next = next;
            _onRequestReceived = onRequestReceived;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;

            // 只记录POST请求
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // 读取请求体
            string body = string.Empty;
            if (request.Body.CanRead)
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }

            // 尝试解析JSON并提取code字段
            string displayBody = body;
            try
            {
                if (!string.IsNullOrEmpty(body))
                {
                    var jsonObj = JObject.Parse(body);
                    if (jsonObj.ContainsKey("code"))
                    {
                        displayBody = jsonObj["code"]?.ToString() ?? body;
                    }
                }
            }
            catch
            {
                // 如果JSON解析失败，使用原始body
                displayBody = body;
            }

            // 构建请求头字符串
            var headers = string.Join("\n", request.Headers.Select(h => $"{h.Key}: {h.Value}"));

            // 创建HTTP请求记录
            var httpRequest = new ScanerServer.Models.HttpRequest
            {
                Method = request.Method,
                Path = request.Path + request.QueryString,
                Headers = headers,
                Body = displayBody,
                Timestamp = DateTime.Now,
                ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                IsCopied = false,
            };

            // 保存到数据库
            using var dbContext = new ApplicationDbContext();
            dbContext.HttpRequests.Add(httpRequest);
            await dbContext.SaveChangesAsync();

            // 通知UI更新
            _onRequestReceived(httpRequest);

            await _next(context);
        }
    }
}
