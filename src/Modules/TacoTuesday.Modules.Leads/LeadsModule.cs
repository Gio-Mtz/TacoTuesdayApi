using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using TacoTuesday.Modules.Leads.Persistence;
using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Leads;

public static class LeadsModule
{
    public const string RateLimitPolicy = "leads-write";

    public const string DatabaseHealthCheck = "leads-db";

    public static IServiceCollection AddLeadsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEndpointsFrom(typeof(LeadsModule).Assembly);
        services.AddHandlersFrom(typeof(LeadsModule).Assembly);

        return services;
    }

    public static IServiceCollection AddLeadsPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.AddDbContext<LeadsDbContext>(configureDatabase);

        services.AddScoped<ILeadStore, EfCoreLeadStore>();

        services.AddHealthChecks()
            .AddDbContextCheck<LeadsDbContext>(
                name: DatabaseHealthCheck,
                tags: ["ready", "db"]);

        return services;
    }
}
