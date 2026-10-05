using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TacoTuesday.Modules.Leads;
using TacoTuesday.Modules.Leads.Persistence;

namespace TacoTuesday.IntegrationTests;

/// <summary>
/// The API with a real database behind it, one per test class.
///
/// **Why SQLite and not the EF in-memory provider.** Half of what <c>POST /api/leads</c>
/// promises is decided by the unique index on <c>NormalizedEmail</c>, and the in-memory
/// provider does not enforce indexes — so the "posting twice succeeds" test would pass
/// against an endpoint with no constraint behind it, which is exactly the regression worth
/// catching.
///
/// **Why the connection is a field.** A SQLite <c>:memory:</c> database exists for as long as
/// the connection that created it is open, and every other connection to <c>:memory:</c> is a
/// different database. Holding it here is what makes the schema survive between requests, and
/// what makes each test class get its own clean list.
///
/// **Why nothing is removed from the container.** <c>Testing:AllowMissingDatabase</c> tells
/// Program.cs that the caller brings its own database, so the host registers none and there is
/// nothing to replace here — this adds, it does not override. That seam exists so that a test
/// never has to guess at the shape of EF Core's internal service registrations, which change
/// between versions.
///
/// ⚠️ This paragraph used to say the host "registers the database only when a connection string
/// is configured". That was accurate and useless: sprint-0 committed a connection string to
/// appsettings.Development.json on purpose, and Development is the environment this factory
/// runs in — so the host registered SQL Server and this call registered SQLite on top. Two
/// registrations of the module meant two readiness checks named <c>leads-db</c>, and a
/// duplicate check name throws while the host is being built, which killed all six tests that
/// boot a host. The host now keys off the flag and not off the string; see Program.cs. The
/// invariant is pinned by <see cref="HealthCheckRegistrationTests"/>.
/// </summary>
public sealed class TacoTuesdayApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>
    /// Opened here rather than inside <see cref="ConfigureWebHost"/> because that callback can
    /// run more than once (any <c>WithWebHostBuilder</c> call re-runs it) and opening an
    /// already-open connection throws.
    /// </summary>
    public TacoTuesdayApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // UseSetting lands in host configuration, which Program.cs reads before it decides
        // whether to refuse to start. This is the whole reason that guard is a setting and
        // not a check on the environment name.
        builder.UseSetting("Testing:AllowMissingDatabase", "true");

        builder.ConfigureTestServices(services =>
            services.AddLeadsPersistence(options => options.UseSqlite(_connection)));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // EnsureCreated, not Migrate. The migration is SQL Server DDL — `uniqueidentifier`,
        // `datetimeoffset`, `nvarchar` — and SQLite cannot run it. Which means: **these tests
        // do not verify the migration.** They verify the model and the endpoint. Whether the
        // migration produces the same schema the model describes is checked by
        // `dotnet ef migrations has-pending-model-changes`, in CI and in docs/database.md.
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<LeadsDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
