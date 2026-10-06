using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Orders.Application.Helpers.Mapper
{
    public static class MyMapperServiceCollectionExtensions
    {
        public static IServiceCollection AddMyMapper(this IServiceCollection services, Assembly assembly)
        {
            var profileTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IMappingProfile).IsAssignableFrom(t));

            foreach (var t in profileTypes)
            {
                services.TryAddEnumerable(
                    ServiceDescriptor.Singleton(typeof(IMappingProfile), t)
                );
            }

            services.TryAddSingleton<IObjectMapper>(sp =>
            {
                var cfg = new MapperConfig();

                foreach (var profile in sp.GetServices<IMappingProfile>())
                    profile.Register(cfg);

                return new ObjectMapper(cfg.GetDefs(), cfg.GetPatchDefs());
            });

            return services;
        }
    }
}
