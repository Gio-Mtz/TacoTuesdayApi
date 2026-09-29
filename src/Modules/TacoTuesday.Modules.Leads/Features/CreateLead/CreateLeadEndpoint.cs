using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Leads.Features.CreateLead;

/// <summary>
/// <c>POST /api/leads</c> — the only call the landing page's waitlist form makes.
/// </summary>
public sealed class CreateLeadEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) => app
        .MapPost("/api/leads", async (
            CreateLeadCommand command,
            CreateLeadHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(command, ct);

            if (!result.IsSuccess)
            {
                return Results.BadRequest(result.Error);
            }

            var response = result.Value!;

            // 201 the first time, 200 when the address was already there. Both are a
            // success and the UI treats them the same — the status code is for caches,
            // logs and whoever reads the access log later. What the form actually reads
            // is `alreadyRegistered`.
            return response.AlreadyRegistered
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status201Created);
        })
        .WithName("CreateLead")
        .WithTags("Leads")
        .WithSummary("Adds an address to the waiting list.")
        .WithDescription(
            "Idempotent by lowercased email: posting the same address twice answers 200 with " +
            "alreadyRegistered=true, never 409. Rate limited per client IP — see ADR 0004.")
        // The form's honeypot is a client-side filter and stops nothing that posts straight
        // at this URL. This is the actual fence. Policy configured in Program.cs.
        .RequireRateLimiting(LeadsModule.RateLimitPolicy);
}
