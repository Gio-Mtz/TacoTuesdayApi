using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.DependencyInjection;

namespace TacoTuesday.SharedKernel;
public static class EndpointExtensions
{
    public static IServiceCollection AddEndpointsFrom(this IServiceCollection services, Assembly assembly)
    {
        services
            .Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(c => c.AssignableTo<IEndpoint>(), publicOnly: false)
            .As<IEndpoint>()
            .WithSingletonLifetime());
        return services;
    }
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();
        foreach(var endpoint in endpoints)
        {
            endpoint.Map(app);
        }
        return app;
    }
}