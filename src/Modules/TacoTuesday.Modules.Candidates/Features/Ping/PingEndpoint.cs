using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Candidates.Features.Ping;

public sealed class PingEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) => app
        .MapGet("/api/candidates/ping", async(string? name, PingHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new PingQuery(name), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithName("CandidatesPing")
        .WithTags("Candidates")
        .WithSummary("Proves the module wiring works. Delete me in US-013");
}