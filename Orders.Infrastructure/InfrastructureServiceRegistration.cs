using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Common.Interfaces;
using Orders.Application.Interfaces;
using Orders.Infrastructure.Services;

namespace Orders.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructureService(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient();
            services.AddSingleton<IEncryptionService, AesEncryptionService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IAppsettingServices, AppsettingServices>();

            // Standard Redis Cache (IDistributedCache) without custom multiplexer
            var redisConnection = configuration.GetValue<string>("ConnectionStrings:RedisConnection");
            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                var instanceName = configuration.GetValue<string>("Redis:InstanceName")
                    ?? $"{configuration.GetValue<string>("Redis:App") ?? "MS-Orders"}:";

                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = instanceName;
                });
            }

            return services;
        }
    }
}
