using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Orders.Application.Common.Interfaces;
using Orders.Application.Constants;
using Orders.Application.Dtos;
using Orders.Application.Helper;
using Orders.Application.Interfaces;

namespace Orders.Api.Middleware
{
    public class CurrentUserMiddleware
    {
        private readonly RequestDelegate _next;

        public CurrentUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ICurrentUserService currentUserService,
            IEncryptionService encryptionService,
            IAppsettingServices appsettingServices,
            IConfiguration configuration)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                currentUserService.UserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.User.FindFirst("sub")?.Value;
                currentUserService.FullName = context.User.FindFirst(ClaimTypes.Name)?.Value
                    ?? context.User.FindFirst("name")?.Value;
                currentUserService.Email = context.User.FindFirst(ClaimTypes.Email)?.Value
                    ?? context.User.FindFirst("email")?.Value;
                currentUserService.EmployeeId = context.User.FindFirst("EmployeeId")?.Value
                    ?? context.User.FindFirst("employee_id")?.Value
                    ?? currentUserService.UserId;
                currentUserService.PhoneNumber = context.User.FindFirst(ClaimTypes.MobilePhone)?.Value
                    ?? context.User.FindFirst("phone_number")?.Value;

                var roles = context.User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
                if (roles.Count > 0)
                {
                    currentUserService.RolesInformation = roles;
                }

                // If "LoginInfo" claim is present (encrypted payload support)
                var loginInfoEncrypted = context.User.FindFirst("LoginInfo")?.Value;
                if (!string.IsNullOrEmpty(loginInfoEncrypted))
                {
                    try
                    {
                        var key = configuration.GetValue<string>("JwtAccess:Key") ?? "DefaultEncryptionKey32Characters!!";

                        var decrypted = encryptionService.Decrypt(loginInfoEncrypted, key);
                        if (!string.IsNullOrEmpty(decrypted))
                        {
                            var loginInfo = JsonSerializer.Deserialize<LoginInfoDto>(decrypted, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (loginInfo != null)
                            {
                                if (!string.IsNullOrEmpty(loginInfo.UserId)) currentUserService.UserId = loginInfo.UserId;
                                if (!string.IsNullOrEmpty(loginInfo.FullName)) currentUserService.FullName = loginInfo.FullName;
                                if (!string.IsNullOrEmpty(loginInfo.Email)) currentUserService.Email = loginInfo.Email;
                                if (!string.IsNullOrEmpty(loginInfo.EmployeeId)) currentUserService.EmployeeId = loginInfo.EmployeeId;
                                if (!string.IsNullOrEmpty(loginInfo.PhoneNumber)) currentUserService.PhoneNumber = loginInfo.PhoneNumber;
                                if (loginInfo.RolesInformation?.Count > 0) currentUserService.RolesInformation = loginInfo.RolesInformation;
                            }
                        }
                    }
                    catch
                    {
                        // Fallback gracefully to standard claims
                    }
                }
            }

            // Extract timezone offset header if available
            if (context.Request.Headers.TryGetValue(GeneralConstant.Header.TimezoneOffset, out var offsetHeader)
                && !string.IsNullOrWhiteSpace(offsetHeader))
            {
                var offset = GeneralHelper.GetTimezoneOffsetInHours(offsetHeader);
                currentUserService.TimeOffSet = offset.TotalHours;
            }

            await _next(context);
        }
    }
}
