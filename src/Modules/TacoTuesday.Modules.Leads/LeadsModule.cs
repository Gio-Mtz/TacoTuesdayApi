using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Name of the readiness check this module contributes. It shows up as a key in the
    /// JSON of <c>/health/ready</c>, so it is part of what an operator reads at 3am.
    /// </summary>
    public const string DatabaseHealthCheck = "leads-db";

    /// <summary>
    /// The endpoints and the handlers. Everything that does not need a database.
    /// </summary>
    public static IServiceCollection AddLeadsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEndpointsFrom(typeof(LeadsModule).Assembly);
        services.AddHandlersFrom(typeof(LeadsModule).Assembly);

        return services;
    }

    /// <summary>
    /// The database-backed waiting list: the context, the store and the readiness check.
    ///
    /// Split from <see cref="AddLeadsModule"/> on purpose, and the seam is where the
    /// connection string lives. **The module owns the model; the host owns the provider.**
    /// Nothing in this project references SQL Server, which is why the store's tests can run
    /// the same code against SQLite and still have a real unique index to violate.
    ///
    /// It also means there is exactly one call to make, and one place to look, when the
    /// answer to "where does this write to?" is needed — instead of a provider chosen
    /// halfway down a module the host cannot see.
    /// </summary>
    /// <param name="configureDatabase">
    /// How to reach the database. In production, <c>UseSqlServer(connectionString)</c> from
    /// Program.cs; in the tests, <c>UseSqlite(connection)</c>.
    /// </param>
    public static IServiceCollection AddLeadsPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.AddDbContext<LeadsDbContext>(configureDatabase);

        // Scoped, following the context. This is the change US-004 said would be one line:
        // the handler depends on ILeadStore and does not know that the list stopped being
        // a dictionary.
        services.AddScoped<ILeadStore, EfCoreLeadStore>();

        // Readiness, not liveness. "Can this process serve a signup?" is a different
        // question from "is this process alive", and the answer is no while the database is
        // unreachable — Container Apps should stop sending traffic, not restart the
        // container. The check runs on /health/ready, which US-002 already mapped.
        services.AddHealthChecks()
            .AddDbContextCheck<LeadsDbContext>(
                name: DatabaseHealthCheck,
                tags: ["ready", "db"]);

        return services;
    }
}
