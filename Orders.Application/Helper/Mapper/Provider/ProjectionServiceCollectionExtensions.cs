using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Orders.Application.Helpers.Mapper.Provider
{
    public static class ProjectionServiceCollectionExtensions
    {
        public static IServiceCollection AddMyProjections(this IServiceCollection services, Assembly assembly)
        {
            // 1) Register all projection profiles
            var profileTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IProjectionProfile).IsAssignableFrom(t));

            foreach (var t in profileTypes)
                services.AddSingleton(typeof(IProjectionProfile), t);

            // 2) Build provider once
            services.AddSingleton<IProjectionProvider>(sp =>
            {
                var registry = new ProjectionRegistry();

                foreach (var profile in sp.GetServices<IProjectionProfile>())
                    profile.Register(registry);

                return new ProjectionProvider(registry.GetAll());
            });

            return services;
        }
    }
}
