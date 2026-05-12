using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Abstractions;

public interface IAggregateTracker
{
    IReadOnlyList<AggregateRoot> TrackedAggregates { get; }
    void Track(AggregateRoot aggregate);
    void Clear();
}
