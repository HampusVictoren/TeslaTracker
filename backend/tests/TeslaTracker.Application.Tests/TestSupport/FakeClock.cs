using TeslaTracker.Application.Abstractions;

namespace TeslaTracker.Application.Tests.TestSupport;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);
}
