using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Candidates.Features.Ping;

public sealed class PingHandler(IClock clock)
{
    public Task<Result<PingResponse>> Handle(PingQuery query, CancellationToken ct)
    {
        if(query.Name is {Length: > 50})
        {
            return Task.FromResult(Result<PingResponse>.Invalid(new ResultError("NAME_TOO_LONG", "Name must be 50 characters or fewer.")));
        }
        var response = new PingResponse(
            Module: "Candidates",
            Message: $"Hello, {query.Name ?? "world"}",
            ServerTimeUtc: clock.UtcNow
        );
        return Task.FromResult(Result<PingResponse>.Success(response));
    }
}