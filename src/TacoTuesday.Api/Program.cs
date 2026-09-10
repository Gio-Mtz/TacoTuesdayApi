using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using TacoTuesday.Infrastructure;
using TacoTuesday.Modules.Candidates;
using TacoTuesday.Modules.Companies;
using TacoTuesday.SharedKernel;

const string CorsPolicy = "tacotuesday-ui";

var builder = WebApplication.CreateBuilder(args);

// ── Platform services ─────────────────────────────────────────────
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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

// ── Modules ───────────────────────────────────────────────────────
// The host knows each module by exactly one method. Nothing else.
builder.Services.AddCandidatesModule();
builder.Services.AddCompaniesModule();

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────
app.UseExceptionHandler();
app.UseStatusCodePages();

// Must run before the endpoints. CORS works by adding response headers, so if it
// runs after the endpoint already produced the response it silently does nothing.
app.UseCors(CorsPolicy);

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