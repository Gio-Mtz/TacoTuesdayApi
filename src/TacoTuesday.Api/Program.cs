using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using TacoTuesday.Infrastructure;
using TacoTuesday.Modules.Candidates;
using TacoTuesday.Modules.Companies;
using TacoTuesday.Modules.Leads;
using TacoTuesday.SharedKernel;

const string CorsPolicy = "tacotuesday-ui";

var builder = WebApplication.CreateBuilder(args);

// ── Platform services ─────────────────────────────────────────────
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// ── Who is the client, really ──────────────────────────────────────
// Behind Container Apps ingress every request arrives from the ingress proxy, so
// RemoteIpAddress is the proxy's address for everybody. Without this, the rate limiter
// below would put the entire internet in one bucket and the first busy minute would
// lock out every visitor at once.
// KnownProxies/KnownNetworks are cleared because the ingress address is assigned by
// Azure and is not knowable at build time. The trade-off is written down in ADR 0004:
// it means trusting X-Forwarded-For, which is only safe because ingress is the single
// way in. The day the container is reachable directly, this has to be revisited.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── CORS ──────────────────────────────────────────────────────────
// The browser blocks a call from origin A to origin B unless B says otherwise.
// In production the UI is its own Container App, so it IS a different origin.
// Origins come from configuration, never hardcoded: dev, staging and prod each
// have their own, and a redeploy is not needed to add one.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    }
}));

// ── Rate limiting ─────────────────────────────────────────────────
// The waitlist form has a honeypot, but a honeypot is a filter on the page, not a
// fence on the endpoint: it stops nothing that POSTs straight at /api/leads. This is
// the fence, and it is a requirement inherited from ADR 0003.
// Limits come from configuration so the integration tests can raise them — the point
// of a test is the behaviour, not the wait.
var leadsRateLimit = builder.Configuration.GetSection("RateLimiting:Leads");
var leadsPermitLimit = leadsRateLimit.GetValue("PermitLimit", 5);
var leadsWindowSeconds = leadsRateLimit.GetValue("WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    // Default is 503, which says "the server is broken". It is not: the client is
    // going too fast. The UI already has a message for 429 and none for 503.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };

    options.AddPolicy(LeadsModule.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = leadsPermitLimit,
            Window = TimeSpan.FromSeconds(leadsWindowSeconds),
            // No queue. Making somebody wait in line for a form they will submit once is
            // worse than telling them straight away to try again in a minute.
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

// ── Modules ───────────────────────────────────────────────────────
// The host knows each module by exactly one method. Nothing else.
builder.Services.AddCandidatesModule();
builder.Services.AddCompaniesModule();
builder.Services.AddLeadsModule();

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────
// First in the pipeline on purpose: everything downstream that asks "who is calling?"
// has to get the real answer, and the answer is only correct after this runs.
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Must run before the endpoints. CORS works by adding response headers, so if it
// runs after the endpoint already produced the response it silently does nothing.
app.UseCors(CorsPolicy);

// After CORS, and that order is load-bearing. The CORS middleware writes its headers on
// the way in, so a 429 produced here still carries them and the browser can read the
// status. Reverse the two and a rate-limited visitor sees an opaque CORS failure — which
// the UI reports as "no pudimos conectar" instead of "demasiados intentos".
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Taco Tuesday API"));
}

// Liveness: "is the process up?" — no dependency checks, so a slow database
// never makes the orchestrator kill a healthy process.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: "can it serve traffic?" — runs every registered check.
// US-002 adds the PostgreSQL check here.
app.MapHealthChecks("/health/ready");

app.MapEndpoints();

app.Run();

// Exposed so WebApplicationFactory<Program> can find it in the integration tests.
public partial class Program;