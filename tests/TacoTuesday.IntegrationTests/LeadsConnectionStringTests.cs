using Shouldly;
using TacoTuesday.Api.Configuration;

namespace TacoTuesday.IntegrationTests;

public sealed class LeadsConnectionStringTests
{
    private const string Parseable =
        "Server=tcp:example.database.windows.net,1433;Initial Catalog=ttc-sqldb-prod;" +
        "Persist Security Info=False;User ID=app;Password=pw;MultipleActiveResultSets=False;" +
        "Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

    [Fact]
    public void A_usable_connection_string_is_accepted()
    {
        Should.NotThrow(() => LeadsConnectionString.ThrowIfUnusable(Parseable, allowMissing: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_connection_string_stops_the_app(string? missing)
    {
        var failure = Should.Throw<InvalidOperationException>(
            () => LeadsConnectionString.ThrowIfUnusable(missing, allowMissing: false));

        failure.Message.ShouldContain(LeadsConnectionString.ConfigurationKey);
        failure.Message.ShouldContain(LeadsConnectionString.EnvironmentVariableName);
    }

    [Fact]
    public void A_missing_connection_string_is_allowed_when_the_caller_brings_its_own_database()
    {
        Should.NotThrow(() => LeadsConnectionString.ThrowIfUnusable(null, allowMissing: true));
    }

    [Fact]
    public void A_connection_string_SqlClient_cannot_parse_is_rejected()
    {
        const string unquotedSemicolonInThePassword =
            "Server=tcp:example.database.windows.net,1433;Initial Catalog=ttc-sqldb-prod;" +
            "User ID=app;Password=pw;word;Encrypt=True;";

        var failure = Should.Throw<InvalidOperationException>(
            () => LeadsConnectionString.ThrowIfUnusable(
                unquotedSemicolonInThePassword, allowMissing: false));

        failure.Message.ShouldContain(LeadsConnectionString.ConfigurationKey);

        failure.InnerException.ShouldBeAssignableTo<ArgumentException>();
        failure.Message.ShouldContain(failure.InnerException!.Message);
    }

    [Fact]
    public void An_unparseable_connection_string_is_rejected_even_when_missing_is_allowed()
    {
        Should.Throw<InvalidOperationException>(
            () => LeadsConnectionString.ThrowIfUnusable(
                "Server=tcp:example.database.windows.net,1433;no-equals-sign-here;",
                allowMissing: true));
    }
}
