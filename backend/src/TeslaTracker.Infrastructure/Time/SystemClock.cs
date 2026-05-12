using TeslaTracker.Application.Abstractions;

namespace TeslaTracker.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
