using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace TacoTuesday.SharedKernel;

public static class ModuleRegistrationExtension
{
    /// <summary>
    /// Registers every *Handler in the assembly as itself, scoped.
    /// This is what replaces MediatR — see ADR-0002.
    /// </summary>
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