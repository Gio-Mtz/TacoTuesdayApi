using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using TacoTuesday.Migrations.SqlServer;
using TacoTuesday.Modules.Leads.Persistence;

namespace TacoTuesday.Api.Data;

/// <summary>
/// How <c>dotnet ef</c> builds a <see cref="LeadsDbContext"/> without starting the app.
///
/// It is needed because the running app only registers the context when a connection string
/// is configured (see Program.cs), and at design time there is none — so without this,
/// <c>dotnet ef migrations add</c> would report that it cannot find the context and the
/// obvious "fix" would be to weaken the startup guard that keeps a misconfigured deploy
/// from booting.
///
/// **The connection string below is never connected to.** <c>migrations add</c> only needs a
/// provider so it can turn the model into SQL Server DDL; it reads no database. The commands
/// that DO touch a database (<c>database update</c>) take the real connection string on the
/// command line — see <c>docs/database.md</c>.
/// </summary>
public sealed class LeadsDbContextFactory : IDesignTimeDbContextFactory<LeadsDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TacoTuesday-DesignTime;Trusted_Connection=True;";

    public LeadsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LeadsDbContext>()
            .UseSqlServer(
                DesignTimeConnectionString,
                // Same pointer as Program.cs, for the same reason: `dotnet ef` has to find the
                // migrations, and they are not in the assembly that holds the context.
                sql => sql.MigrationsAssembly(SqlServerMigrations.AssemblyName))
            .Options;

        return new LeadsDbContext(options);
    }
}
