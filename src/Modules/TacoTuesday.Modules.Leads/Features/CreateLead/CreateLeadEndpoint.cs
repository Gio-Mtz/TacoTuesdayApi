using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Leads.Features.CreateLead;

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

        .RequireRateLimiting(LeadsModule.RateLimitPolicy);
}
