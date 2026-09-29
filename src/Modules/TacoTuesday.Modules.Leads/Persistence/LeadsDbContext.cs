using Microsoft.EntityFrameworkCore;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

/// <summary>
/// The Leads module's slice of the database, and the only type in the solution that is
/// allowed to know that the waiting list is a table.
///
/// **No schema.** Schema-per-module is the tidier boundary and it is deliberately not here
/// yet: SQLite has no schemas, the unit tests for <see cref="EfCoreLeadStore"/> run on
/// SQLite precisely so the unique index is real, and a mapping that only works on one
/// provider would mean the duplicate path is never exercised by a test. Adding the schema
/// later is a rename in a migration, and it costs nothing while the table is empty.
/// See ADR 0005.
///
/// **No <c>Migrate()</c> on startup.** The Container App can run several replicas, and
/// migrating from inside the app means whichever replica boots first takes a schema lock
/// while the others fail their readiness probe. It also means the app's SQL login needs
/// DDL rights forever, to do something that happens once per release. Migrations are a
/// deploy step, not a startup step — the commands are in <c>docs/database.md</c>.
/// </summary>
public sealed class LeadsDbContext(DbContextOptions<LeadsDbContext> options) : DbContext(options)
{
    public DbSet<Lead> Leads => Set<Lead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new LeadConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
