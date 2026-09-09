namespace TacoTuesday.Modules.Candidates.Features.Ping;
public sealed record PingQuery(string? Name);
public sealed record PingResponse(string Module, string Message, DateTimeOffset ServerTimeUtc);