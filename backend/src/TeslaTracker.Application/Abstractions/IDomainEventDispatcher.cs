using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken);
}
