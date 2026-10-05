using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace TacoTuesday.IntegrationTests;

public sealed class HealthEndpointTests(TacoTuesdayApiFactory factory)
    : IClassFixture<TacoTuesdayApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_endpoint_returns_healthy()
    {
        var response = await _client.GetAsync("/health/live", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_endpoint_is_healthy_when_the_database_is_reachable()
    {
        var response = await _client.GetAsync("/health/ready", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        body.ShouldBe("Healthy");
    }

    [Fact]
    public async Task Candidates_ping_endpoint_is_mapped_by_convention()
    {
        var response = await _client.GetAsync("/api/candidates/ping?name=Gio", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PingResponseDto>(CancellationToken.None);
        body!.Module.ShouldBe("Candidates");
        body.Message.ShouldBe("Hello, Gio");
    }

    private sealed record PingResponseDto(string Module, string Message, DateTimeOffset ServerTimeUtc);
}