using Shouldly;
using TacoTuesday.Api.Configuration;

namespace TacoTuesday.IntegrationTests;

/// <summary>
/// The boot guard on <c>ConnectionStrings:Leads</c>.
/// </summary>
/// <remarks>
/// These run without a host, a client or a database: the guard is a pure function of a string,
/// and booting a <c>WebApplicationFactory</c> four times to ask it four questions would be slower
/// and would say less about which input produced which answer.
/// <para>
/// The case that matters is <see cref="A_connection_string_SqlClient_cannot_parse_is_rejected"/>.
/// Before TD-010 the guard only checked for absence, so a malformed value started the app, and
/// the clean start was read as proof the configuration was good while every signup returned 500.
/// </para>
/// </remarks>
public sealed class LeadsConnectionStringTests
{
    /// <summary>
    /// The shape Azure SQL hands out, and the one the runbook tells the operator to paste.
    /// Parseable, and deliberately pointing nowhere real: nothing here opens a connection.
    /// </summary>
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

        // The operator reading this in container logs needs the key and the spelling, not a
        // stack trace. The environment-variable form is the one that is easy to get wrong.
        failure.Message.ShouldContain(LeadsConnectionString.ConfigurationKey);
        failure.Message.ShouldContain(LeadsConnectionString.EnvironmentVariableName);
    }

    /// <summary>
    /// The integration tests supply their own database after the host is built, so an absent
    /// value has to be survivable. That is the whole extent of the opt-out.
    /// </summary>
    [Fact]
    public void A_missing_connection_string_is_allowed_when_the_caller_brings_its_own_database()
    {
        Should.NotThrow(() => LeadsConnectionString.ThrowIfUnusable(null, allowMissing: true));
    }

    /// <summary>
    /// TD-010, and the reason it exists. The unquoted ';' inside the password ends the value
    /// early, so the parser reads <c>Trusted</c> as the start of a keyword and fails. This is
    /// the live-fire shape of the 5-oct outage, reduced to the one character that caused it.
    /// </summary>
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

        // SqlClient's own words are kept, because they carry the character index and that index
        // is the single most useful thing the operator gets. Losing it to a tidier message would
        // throw away the only part that points at the broken character.
        failure.InnerException.ShouldBeAssignableTo<ArgumentException>();
        failure.Message.ShouldContain(failure.InnerException!.Message);
    }

    /// <summary>
    /// The opt-out excuses absence, not garbage. A test flag that waved through an unparseable
    /// string would reintroduce exactly the blind spot TD-010 closes.
    /// </summary>
    [Fact]
    public void An_unparseable_connection_string_is_rejected_even_when_missing_is_allowed()
    {
        Should.Throw<InvalidOperationException>(
            () => LeadsConnectionString.ThrowIfUnusable(
                "Server=tcp:example.database.windows.net,1433;no-equals-sign-here;",
                allowMissing: true));
    }
}
