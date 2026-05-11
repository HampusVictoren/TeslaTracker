namespace TeslaTracker.Domain.SeedWork;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _pendingEvents = [];

    public IReadOnlyList<IDomainEvent> PendingEvents => _pendingEvents;

    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _pendingEvents.Add(domainEvent);
    }

    public void ClearPendingEvents() => _pendingEvents.Clear();
}
