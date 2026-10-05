using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using TacoTuesday.Migrations.SqlServer;
using TacoTuesday.Modules.Leads.Persistence;

namespace TacoTuesday.Api.Data;

public sealed class LeadsDbContextFactory : IDesignTimeDbContextFactory<LeadsDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TacoTuesday-DesignTime;Trusted_Connection=True;";

    public LeadsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LeadsDbContext>()
            .UseSqlServer(
                DesignTimeConnectionString,

                sql => sql.MigrationsAssembly(SqlServerMigrations.AssemblyName))
            .Options;

        return new LeadsDbContext(options);
    }
}
