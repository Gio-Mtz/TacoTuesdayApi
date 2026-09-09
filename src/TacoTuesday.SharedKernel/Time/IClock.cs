namespace TacoTuesday.SharedKernel;

/// <summary>
/// The ONLY source of "now" in domain and application code.
/// Never call DateTimeOffset.UtcNow directly — see Coding-Standards-dotnet.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}