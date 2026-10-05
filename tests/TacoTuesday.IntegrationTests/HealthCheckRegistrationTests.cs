using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

using TacoTuesday.Modules.Leads;

namespace TacoTuesday.IntegrationTests;

/// <summary>
/// One assertion, and it exists because its absence cost six stack traces.
///
/// Every other test in this project boots a host, so every one of them already fails when a
/// health check is registered twice — but they fail with
/// <c>ArgumentException: Duplicate health checks were registered with the name(s): leads-db</c>
/// raised inside host construction, six times, naming six features that are all fine. The
/// cause is not in any of them, so reading the six reports is a detour.
///
/// This test fails for the same reason and says so in its name instead. It is also the only
/// test that states the invariant as an invariant: the module contributes exactly one
/// readiness check, however many times the host and the test fixture each think they are the
/// one wiring up the database.
/// </summary>
public sealed class HealthCheckRegistrationTests(TacoTuesdayApiFactory factory)
    : IClassFixture<TacoTuesdayApiFactory>
{
    [Fact]
    public void The_database_readiness_check_is_registered_exactly_once()
    {
        var registrations = factory.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;

        registrations
            .Count(registration => registration.Name == LeadsModule.DatabaseHealthCheck)
            .ShouldBe(1);
    }
}
