using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace TacoTuesday.IntegrationTests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_endpoint_returns_healthy()
    {
        var response = await _client.GetAsync("/health/live", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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