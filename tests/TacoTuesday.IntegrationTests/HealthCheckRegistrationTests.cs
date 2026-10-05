using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

using TacoTuesday.Modules.Leads;

namespace TacoTuesday.IntegrationTests;

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
