using System.Net;
using System.Text.Json;
using Orders.Application.Common.Exceptions;
using Orders.Application.Common.Models;

namespace Orders.Api.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = HttpStatusCode.InternalServerError;
            ApiResponse<object> response;

            switch (exception)
            {
                case ValidationException validationEx:
                    statusCode = HttpStatusCode.BadRequest;
                    var errors = validationEx.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}").ToList();
                    response = ApiResponse<object>.Fail("Validation failed", "400", errors);
                    break;

                case NotFoundException notFoundEx:
                    statusCode = HttpStatusCode.NotFound;
                    response = ApiResponse<object>.Fail(notFoundEx.Message, "404");
                    break;

                case ForbiddenException forbiddenEx:
                    statusCode = HttpStatusCode.Forbidden;
                    response = ApiResponse<object>.Fail(forbiddenEx.Message, "403");
                    break;

                case Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
                    statusCode = HttpStatusCode.Conflict;
                    response = ApiResponse<object>.Fail("The record was modified or updated concurrently by another transaction. Please reload and try again.", "409");
                    break;

                case ConflictException conflictEx:
                    statusCode = HttpStatusCode.Conflict;
                    response = ApiResponse<object>.Fail(conflictEx.Message, "409");
                    break;

                case UnprocessableEntityException unprocessableEx:
                    statusCode = HttpStatusCode.UnprocessableEntity;
                    response = ApiResponse<object>.Fail(unprocessableEx.Message, "422");
                    break;

                case BadRequestException badRequestEx:
                    statusCode = HttpStatusCode.BadRequest;
                    response = ApiResponse<object>.Fail(badRequestEx.Message, "400");
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    response = ApiResponse<object>.Fail("An unexpected error occurred.", "500");
                    break;
            }

            context.Response.StatusCode = (int)statusCode;
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}
