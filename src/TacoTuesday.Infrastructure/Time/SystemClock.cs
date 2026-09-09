using TacoTuesday.SharedKernel;

namespace TacoTuesday.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}