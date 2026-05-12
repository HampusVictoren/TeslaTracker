using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Infrastructure.Storage;

internal sealed class AggregateTracker : IAggregateTracker
{
    private readonly List<AggregateRoot> _aggregates = [];

    public IReadOnlyList<AggregateRoot> TrackedAggregates => _aggregates;

    public void Track(AggregateRoot aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (!_aggregates.Contains(aggregate))
        {
            _aggregates.Add(aggregate);
        }
    }

    public void Clear() => _aggregates.Clear();
}
