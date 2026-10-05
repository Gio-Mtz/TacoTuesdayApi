using Microsoft.Data.SqlClient;

namespace TacoTuesday.Api.Configuration;

internal static class LeadsConnectionString
{
    internal const string ConfigurationKey = "ConnectionStrings:Leads";

    internal const string EnvironmentVariableName = "ConnectionStrings__Leads";

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
