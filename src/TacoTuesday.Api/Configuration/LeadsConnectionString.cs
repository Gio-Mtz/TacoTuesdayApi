using Microsoft.Data.SqlClient;

namespace TacoTuesday.Api.Configuration;

/// <summary>
/// The boot-time check on <c>ConnectionStrings:Leads</c>.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the check it replaces only asked whether the connection string was
/// <em>there</em>, and that is not the same question as whether it can be used. On 5-oct-2026 a
/// malformed value cost a week of diagnosis: the string was present, so the app started, logged
/// <c>Application started</c>, and passed liveness — and that clean start was then read, across
/// two work blocks, as evidence that the connection string was fine. It was not. SqlClient could
/// not parse it, so every <c>SqlConnection</c> it built threw
/// <c>ArgumentException: Format of the initialization string does not conform to specification
/// starting at index 130</c> before opening a socket. The visible symptoms were a 503 on
/// <c>/health/ready</c> and a 500 on the first signup — which is the precise outcome the old
/// check's own comment said it existed to prevent.
/// </para>
/// <para>
/// So the rule is: a connection string the process cannot use is a misconfiguration, and the
/// cheapest place to find out is a container that refuses to start. Parsing is the strongest
/// check available without touching the network — <see cref="SqlConnectionStringBuilder"/> reads
/// the text and nothing else, so this stays safe to run at boot, offline, and in CI.
/// </para>
/// <para>
/// What this still cannot catch: a string that parses and points at the wrong server, the wrong
/// database, or a login without rights. Those need a connection, and the thing that answers them
/// is <c>/health/ready</c>, which runs <c>CanConnectAsync()</c>. The division is deliberate —
/// boot rejects what is unusable on its face, readiness reports what needs the network.
/// </para>
/// </remarks>
internal static class LeadsConnectionString
{
    /// <summary>The configuration key, spelled as .NET configuration sees it.</summary>
    internal const string ConfigurationKey = "ConnectionStrings:Leads";

    /// <summary>
    /// The same key spelled as an environment variable. Two underscores is how .NET writes a
    /// nested key, and getting it wrong is silent: the app reads no value and reports it missing.
    /// </summary>
    internal const string EnvironmentVariableName = "ConnectionStrings__Leads";

    /// <summary>
    /// Throws if the configured connection string is absent or cannot be parsed.
    /// </summary>
    /// <param name="connectionString">The configured value, or <c>null</c> when unset.</param>
    /// <param name="allowMissing">
    /// Excuses an <em>absent</em> value only — the integration tests bring their own database in
    /// <c>ConfigureTestServices</c>, which runs after this. It does not excuse an unparseable one:
    /// nothing legitimately configures a connection string that SqlClient cannot read, so letting
    /// that through under a test flag would only hide the bug this method exists to surface.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The value is missing and <paramref name="allowMissing"/> is <c>false</c>, or the value is
    /// set and cannot be parsed. Both messages name the key and say what to do next.
    /// </exception>
    internal static void ThrowIfUnusable(string? connectionString, bool allowMissing)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (allowMissing)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{ConfigurationKey} is not configured. Set it as an environment variable " +
                $"({EnvironmentVariableName}) or as a Container Apps secret. " +
                "See docs/database.md.");
        }

        try
        {
            // Parses the text and stops there: no server is named to, no socket is opened, no
            // credential is used. Discarding the result is the point — the question is whether
            // the parse throws.
            _ = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException parseFailure)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} is set, but SqlClient cannot parse it. Every request that " +
                "touches the database would fail on this same error before reaching the network, " +
                "and /health/ready would answer 503. SqlClient said: " +
                $"\"{parseFailure.Message}\"{Environment.NewLine}" +
                "When that message ends in an index, it is the character position in the " +
                "connection string where parsing stopped, counted from 0 — go look at that " +
                "character. The three causes that produce it, in the order they turn up: a value " +
                "containing ';' or '=' that is not wrapped in single quotes (passwords, most " +
                "often); a newline or stray quote picked up while copying the value in; or a " +
                "'{placeholder}' from the portal's ADO.NET template left unreplaced. " +
                "See docs/database.md.",
                parseFailure);
        }
    }
}
