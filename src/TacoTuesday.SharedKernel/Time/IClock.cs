namespace TacoTuesday.SharedKernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}