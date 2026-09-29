namespace TacoTuesday.Migrations.SqlServer;

/// <summary>
/// Marker for the assembly that holds the SQL Server migrations.
///
/// EF Core looks for migrations in the assembly that contains the <c>DbContext</c> unless it
/// is told otherwise, and here it has to be told otherwise: the context lives in
/// <c>TacoTuesday.Modules.Leads</c>, which is not allowed to reference a provider, so the
/// generated snapshot cannot live there (see the comment in this project's .csproj).
///
/// Both places that build options — <c>Program.cs</c> at runtime and
/// <c>LeadsDbContextFactory</c> at design time — pass <see cref="AssemblyName"/> to
/// <c>MigrationsAssembly</c>. Doing it through this type instead of a string literal means
/// that renaming the project is a compile error here, rather than a "no migrations were
/// found" halfway through a deploy.
/// </summary>
public static class SqlServerMigrations
{
    /// <summary>
    /// The simple assembly name, which is the form <c>MigrationsAssembly</c> expects.
    /// </summary>
    public static string AssemblyName { get; } =
        typeof(SqlServerMigrations).Assembly.GetName().Name!;
}
