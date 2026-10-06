using Orders.Application.Interfaces;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Orders.Api.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
        private readonly IAppsettingServices _appsettingServices;

        public RequestResponseLoggingMiddleware(RequestDelegate next
            , ILogger<RequestResponseLoggingMiddleware> logger
            , IAppsettingServices appsettingServices)
        {
            _next = next;
            _logger = logger;
            _appsettingServices = appsettingServices;
        }

        public async Task Invoke(HttpContext context)
        {
            var excludeList = _appsettingServices.GetExcludeLog().Path;
            // Skip excluded paths
            if (excludeList.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            context.Request.EnableBuffering();

            // Read Request Body
            var requestBody = string.Empty;
            if (context.Request.ContentLength > 0 && context.Request.Body.CanRead)
            {
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            // Save response
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            var sw = Stopwatch.StartNew();
            await _next(context);
            sw.Stop();

            // Read response
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            var maskedRequestBody = MaskFields(requestBody);
            var maskedResponseBody = MaskFields(responseBody);

            // Set log level base on status code
            var logLevel = context.Response.StatusCode >= 500 ? LogLevel.Error :
                           context.Response.StatusCode >= 400 ? LogLevel.Warning :
                           LogLevel.Information;

            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?? context.Connection.RemoteIpAddress?.ToString();

            _logger.Log(logLevel, "HTTP Log: {@RequestLog}", new
            {
                Path = context.Request.Path,
                Method = context.Request.Method,
                RequestBody = maskedRequestBody,
                ResponseBody = maskedResponseBody,
                StatusCode = context.Response.StatusCode,
                Ip = ip,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            });

            // Return response to original stream
            await responseBodyStream.CopyToAsync(originalBodyStream);
        }
        private string MaskFields(string body)
        {
            if (string.IsNullOrEmpty(body)) return body;

            var fieldsToMask = _appsettingServices.GetMaskingOptions().FieldsToMask;

            foreach (var field in fieldsToMask)
            {
                body = Regex.Replace(
                    body,
                    $"(\"{Regex.Escape(field)}\"\\s*:\\s*\")(.*?)(\")",
                    match =>
                    {
                        var value = match.Groups[2].Value;
                        if (value.Length <= 4)
                            return $"{match.Groups[1].Value}{new string('*', value.Length)}{match.Groups[3].Value}";

                        // get firt & last 2-4 char
                        int visibleStart = Math.Min(4, value.Length / 3);
                        int visibleEnd = Math.Min(4, value.Length / 3);
                        var start = value.Substring(0, visibleStart);
                        var end = value.Substring(value.Length - visibleEnd);
                        var masked = new string('*', value.Length - (visibleStart + visibleEnd));

                        return $"{match.Groups[1].Value}{start}{masked}{end}{match.Groups[3].Value}";
                    },
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(2)
                );
            }
            return body;
        }
    }
}
