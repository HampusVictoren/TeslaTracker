using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Infrastructure.Storage;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly IAggregateTracker _tracker;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWork(IAggregateTracker tracker, IDomainEventDispatcher dispatcher)
    {
        _tracker = tracker;
        _dispatcher = dispatcher;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var aggregates = _tracker.TrackedAggregates.ToArray();
        var events = aggregates.SelectMany(a => a.PendingEvents).ToList();

        if (events.Count > 0)
        {
            await _dispatcher.DispatchAsync(events, cancellationToken);
        }

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearPendingEvents();
        }

        _tracker.Clear();
    }
}
