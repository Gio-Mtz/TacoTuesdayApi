using System.Net;
using System.Net.Http.Json;

using Shouldly;

namespace TacoTuesday.IntegrationTests;

/// <summary>
/// <c>POST /api/leads</c> through the real pipeline: routing, model binding, the rate
/// limiter, the JSON serializer — and, since US-005, a real database, on SQLite, with the
/// real unique index. See <see cref="TacoTuesdayApiFactory"/> for why that provider.
///
/// **These tests are on a budget.** The endpoint is rate limited to 5 requests per minute
/// per client, and through TestServer every request looks like the same client. xUnit gives
/// each test class its own fixture instance, so this class has its own five — and it uses
/// three. Adding a fourth POST here is fine; a sixth will start failing with 429 and the
/// failure will look like a bug in the endpoint. That is why the rate limit gets its own
/// class below, with its own budget.
/// </summary>
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

        // Same address, different case. The visitor did nothing wrong, so this is a
        // success — 200, not 409 — and the payload is what tells the form to say
        // "ya estabas en la lista" instead of congratulating them twice.
        var second = await _client.PostAsJsonAsync(
            "/api/leads",
            new { kind = "company", name = "Ana", email = "ANA@ACME.COM", company = "Acme", role = (string?)null },
            CancellationToken.None);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var repeated = await second.Content.ReadFromJsonAsync<LeadResponseDto>(CancellationToken.None);
        repeated!.AlreadyRegistered.ShouldBeTrue();
        repeated.Id.ShouldBe(created.Id);

        // The UI reads `alreadyRegistered`, spelled exactly like this. A serializer set to
        // PascalCase would keep every assertion above green and still break the form.
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

/// <summary>
/// The rate limit, in its own class so it gets its own fixture — and therefore its own
/// budget — instead of eating the one the tests above are counting on.
///
/// This is the requirement inherited from ADR 0003: the form's honeypot is a filter drawn
/// on the page and stops nothing that posts straight at the URL.
/// </summary>
public sealed class LeadsRateLimitTests(TacoTuesdayApiFactory factory)
    : IClassFixture<TacoTuesdayApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task The_sixth_request_in_a_minute_is_refused_with_429()
    {
        // appsettings.json: 5 per 60 seconds. Five distinct, valid addresses go through.
        for (var i = 0; i < 5; i++)
        {
            var allowed = await PostLead($"lead{i}@acme.com");

            allowed.StatusCode.ShouldBe(
                HttpStatusCode.Created,
                $"request {i + 1} of 5 should still be inside the limit");
        }

        var refused = await PostLead("uno-de-mas@acme.com");

        // 429, not 503. The UI has a sentence for "demasiados intentos" and none for
        // "el servidor está caído", and the two are not the same thing to tell a visitor.
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // And it says when to come back, instead of leaving the client to guess.
        refused.Headers.RetryAfter.ShouldNotBeNull();
    }

    private Task<HttpResponseMessage> PostLead(string email) => _client.PostAsJsonAsync(
        "/api/leads",
        new { kind = "company", name = "Ana", email, company = "Acme", role = (string?)null },
        CancellationToken.None);
}
