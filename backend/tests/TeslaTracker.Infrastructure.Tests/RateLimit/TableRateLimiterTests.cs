using FluentAssertions;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Infrastructure.RateLimit;
using TeslaTracker.Infrastructure.Tests.TestSupport;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.RateLimit;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);
}

[Collection("Azurite")]
public class TableRateLimiterTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _fixture;

    public TableRateLimiterTests(AzuriteFixture fixture) => _fixture = fixture;

    private TableRateLimiter CreateLimiter(IClock clock) => new(_fixture.ServiceClient, clock);

    [RequiresAzuriteFact]
    public async Task First_Acquire_Succeeds()
    {
        var clock = new FakeClock();
        var limiter = CreateLimiter(clock);

        var acquired = await limiter.TryAcquireAsync($"ip:test:{Guid.NewGuid()}", 3, CancellationToken.None);

        acquired.Should().BeTrue();
    }

    [RequiresAzuriteFact]
    public async Task Acquire_Beyond_Max_Returns_False()
    {
        var clock = new FakeClock();
        var limiter = CreateLimiter(clock);
        var key = $"ip:test:{Guid.NewGuid()}";

        for (var i = 0; i < 3; i++)
        {
            (await limiter.TryAcquireAsync(key, 3, CancellationToken.None)).Should().BeTrue();
        }

        var blocked = await limiter.TryAcquireAsync(key, 3, CancellationToken.None);

        blocked.Should().BeFalse();
    }

    [RequiresAzuriteFact]
    public async Task Next_Minute_Bucket_Resets_Counter()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 5, 11, 12, 0, 0, TimeSpan.Zero) };
        var limiter = CreateLimiter(clock);
        var key = $"ip:test:{Guid.NewGuid()}";

        (await limiter.TryAcquireAsync(key, 1, CancellationToken.None)).Should().BeTrue();
        (await limiter.TryAcquireAsync(key, 1, CancellationToken.None)).Should().BeFalse();

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        (await limiter.TryAcquireAsync(key, 1, CancellationToken.None)).Should().BeTrue();
    }
}
