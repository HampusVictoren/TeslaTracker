using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders.Specifications;

public sealed class DueForSyncSpec : ISpecification<Order>
{
    public static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(50);

    private readonly DateTimeOffset _now;

    public DueForSyncSpec(DateTimeOffset now) => _now = now;

    public bool IsSatisfiedBy(Order candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.IsActive)
        {
            return false;
        }

        return _now - candidate.LastSyncedAt >= SyncInterval;
    }
}
