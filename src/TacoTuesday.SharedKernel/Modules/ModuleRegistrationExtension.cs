using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace TacoTuesday.SharedKernel;

public static class ModuleRegistrationExtension
{
    public static IServiceCollection AddHandlersFrom(this IServiceCollection services, Assembly assembly)
    {
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(c => c.Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal)))
            .AsSelf().
            WithScopedLifetime());

            return services;
    }
}