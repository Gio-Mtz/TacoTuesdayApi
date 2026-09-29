using Microsoft.Extensions.DependencyInjection;

using TacoTuesday.Modules.Leads.Persistence;
using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Leads;

public static class LeadsModule
{
    /// <summary>
    /// Name of the rate limiting policy this module's write endpoint requires.
    /// The policy itself is configured by the host (Program.cs) because partitioning
    /// needs the HttpContext, and a handler that can see the HttpContext has stopped
    /// being a handler. The host owns the pipeline; the module owns the name.
    /// </summary>
    public const string RateLimitPolicy = "leads-write";

    public static IServiceCollection AddLeadsModule(this IServiceCollection services)
    {
        services.AddEndpointsFrom(typeof(LeadsModule).Assembly);
        services.AddHandlersFrom(typeof(LeadsModule).Assembly);

        // Singleton on purpose: the whole point of the store is that it remembers
        // between requests. US-005 replaces this registration with the EF Core one
        // and nothing else in the module changes — that is why the handler depends
        // on the interface and never on this type.
        services.AddSingleton<ILeadStore, InMemoryLeadStore>();

        return services;
    }
}
