using System.Net;
using System.Net.Http.Json;

using Shouldly;

namespace TacoTuesday.IntegrationTests;

public sealed class LeadsEndpointTests(TacoTuesdayApiFactory factory)
    : IClassFixture<TacoTuesdayApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Posting_the_same_address_twice_succeeds_both_times_and_says_so()
    {
        var lead = new
        {
            kind = "company",
            name = "Ana López",
            email = "ana@acme.com",
            company = "Acme",
            role = (string?)null
        };

        var first = await _client.PostAsJsonAsync("/api/leads", lead, CancellationToken.None);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await first.Content.ReadFromJsonAsync<LeadResponseDto>(CancellationToken.None);
        created!.AlreadyRegistered.ShouldBeFalse();
        created.Id.ShouldNotBe(Guid.Empty);

        var second = await _client.PostAsJsonAsync(
            "/api/leads",
            new { kind = "company", name = "Ana", email = "ANA@ACME.COM", company = "Acme", role = (string?)null },
            CancellationToken.None);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var repeated = await second.Content.ReadFromJsonAsync<LeadResponseDto>(CancellationToken.None);
        repeated!.AlreadyRegistered.ShouldBeTrue();
        repeated.Id.ShouldBe(created.Id);

        var raw = await second.Content.ReadAsStringAsync(CancellationToken.None);
        raw.ShouldContain("\"alreadyRegistered\"");
        raw.ShouldContain("\"id\"");
    }

    [Fact]
    public async Task A_body_that_breaks_the_rules_is_a_400_with_the_offending_fields_named()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/leads",
            new { kind = "empresa", name = "", email = "no-es-un-correo", company = (string?)null, role = (string?)null },
            CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        body.ShouldContain("VALIDATION_FAILED");
        body.ShouldContain("kind");
        body.ShouldContain("name");
        body.ShouldContain("email");
    }

    private sealed record LeadResponseDto(Guid Id, bool AlreadyRegistered);
}

public sealed class LeadsRateLimitTests(TacoTuesdayApiFactory factory)
    : IClassFixture<TacoTuesdayApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task The_sixth_request_in_a_minute_is_refused_with_429()
    {
        for (var i = 0; i < 5; i++)
        {
            var allowed = await PostLead($"lead{i}@acme.com");

            allowed.StatusCode.ShouldBe(
                HttpStatusCode.Created,
                $"request {i + 1} of 5 should still be inside the limit");
        }

        var refused = await PostLead("uno-de-mas@acme.com");

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        refused.Headers.RetryAfter.ShouldNotBeNull();
    }

    private Task<HttpResponseMessage> PostLead(string email) => _client.PostAsJsonAsync(
        "/api/leads",
        new { kind = "company", name = "Ana", email, company = "Acme", role = (string?)null },
        CancellationToken.None);
}
