using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Common.Behaviors;
using Orders.Application.Helpers.Mapper;
using Orders.Application.Profile.Mapping;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Orders.Test")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace Orders.Application
{
    public static class ApplicationServiceRegistration
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            var assembly = typeof(ApplicationServiceRegistration).Assembly;

            // MediatR
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(assembly);
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            });

            // FluentValidation
            services.AddValidatorsFromAssembly(assembly);

            // Custom Mapper
            services.AddMyMapper(typeof(AuditTrailMappingProfile).Assembly);

            return services;
        }
    }
}
