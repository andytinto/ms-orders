using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Common.Interfaces;
using Orders.Application.Interfaces;
using Orders.Persistance.DbContext;

namespace Orders.Persistance
{
    public static class PersistenceServiceRegistration
    {
        public static IServiceCollection AddPersistenceService(this IServiceCollection services)
        {
            services.AddDbContext<OrdersContext>((serviceProvider, options) =>
            {
                var appsettingServices = serviceProvider.GetRequiredService<IAppsettingServices>();
                var connStringOptions = appsettingServices.GetConnectionStringsOptions();

                string connString = !string.IsNullOrEmpty(connStringOptions?.OrdersConnectionString)
                    ? connStringOptions.OrdersConnectionString
                    : connStringOptions?.CMSConnectionString ?? string.Empty;

                if (!string.IsNullOrEmpty(connString))
                {
                    options.UseSqlServer(connString);
                }
                else
                {
                    options.UseInMemoryDatabase("OrdersDb");
                }
            });

            services.AddScoped<IOrdersDbContext>(sp => sp.GetRequiredService<OrdersContext>());

            return services;
        }
    }
}