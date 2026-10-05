using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TacoTuesday.Api.Configuration;
using TacoTuesday.Infrastructure;
using TacoTuesday.Migrations.SqlServer;
using TacoTuesday.Modules.Candidates;
using TacoTuesday.Modules.Companies;
using TacoTuesday.Modules.Leads;
using TacoTuesday.SharedKernel;

const string CorsPolicy = "tacotuesday-ui";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

var leadsRateLimit = builder.Configuration.GetSection("RateLimiting:Leads");
var leadsPermitLimit = leadsRateLimit.GetValue("PermitLimit", 5);
var leadsWindowSeconds = leadsRateLimit.GetValue("WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
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

            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var leadsConnectionString = builder.Configuration.GetConnectionString("Leads");

var allowMissingDatabase = builder.Configuration.GetValue("Testing:AllowMissingDatabase", false);

LeadsConnectionString.ThrowIfUnusable(leadsConnectionString, allowMissingDatabase);

builder.Services.AddCandidatesModule();
builder.Services.AddCompaniesModule();
builder.Services.AddLeadsModule();

if (!allowMissingDatabase && !string.IsNullOrWhiteSpace(leadsConnectionString))
{
    builder.Services.AddLeadsPersistence(options => options.UseSqlServer(
        leadsConnectionString,
        sql =>
        {
            sql.MigrationsAssembly(SqlServerMigrations.AssemblyName);

            sql.EnableRetryOnFailure(
                maxRetryCount: 5,

                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));
}

var app = builder.Build();

app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors(CorsPolicy);

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Taco Tuesday API"));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.MapHealthChecks("/health/ready");

app.MapEndpoints();

app.Run();

public partial class Program;