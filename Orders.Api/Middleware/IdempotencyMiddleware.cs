using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Interfaces;
using Orders.Application.Common.Models;
using Orders.Domain.Models;

namespace Orders.Api.Middleware
{
    public class IdempotencyMiddleware
    {
        public const string IdempotencyHeader = "Idempotency-Key";
        private readonly RequestDelegate _next;
        private readonly ILogger<IdempotencyMiddleware> _logger;

        public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
            var isCreateOrder = context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                                && path.Equals("/api/orders", StringComparison.OrdinalIgnoreCase);

            if (!isCreateOrder)
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var headerValue) ||
                string.IsNullOrWhiteSpace(headerValue))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                var badResponse = ApiResponse<object>.Fail("The 'Idempotency-Key' request header is required for creating an order.", "400");
                await context.Response.WriteAsync(JsonSerializer.Serialize(badResponse));
                return;
            }

            var idempotencyKey = headerValue.ToString().Trim();

            // Read request body to compute hash
            context.Request.EnableBuffering();
            string requestBody;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            var requestHash = ComputeSha256Hash(requestBody);

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IOrdersDbContext>();

            var existingRecord = await dbContext.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == idempotencyKey);

            if (existingRecord != null)
            {
                if (existingRecord.RequestHash != requestHash)
                {
                    _logger.LogWarning("Idempotency key {Key} replayed with payload mismatch.", idempotencyKey);
                    context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                    context.Response.ContentType = "application/json";
                    var conflictResponse = ApiResponse<object>.Fail("Idempotency-Key was previously used with a different request payload.", "422");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(conflictResponse));
                    return;
                }

                _logger.LogInformation("Serving cached response for idempotency key {Key}", idempotencyKey);
                context.Response.StatusCode = existingRecord.StatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(existingRecord.ResponseBody);
                return;
            }

            // Capture response to cache it
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            try
            {
                await _next(context);

                responseBodyStream.Position = 0;
                var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
                responseBodyStream.Position = 0;

                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
                {
                    try
                    {
                        var newRecord = new IdempotencyRecord
                        {
                            Key = idempotencyKey,
                            RequestHash = requestHash,
                            StatusCode = context.Response.StatusCode,
                            ResponseBody = responseBody,
                            CreatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddHours(24)
                        };

                        dbContext.IdempotencyRecords.Add(newRecord);
                        await dbContext.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to persist IdempotencyRecord for key {Key} (possibly concurrent insert).", idempotencyKey);
                    }
                }

                await responseBodyStream.CopyToAsync(originalBodyStream);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private static string ComputeSha256Hash(string rawData)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
