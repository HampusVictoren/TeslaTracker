using Microsoft.Extensions.DependencyInjection;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Infrastructure.DomainEvents;

internal sealed class InMemoryDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _provider;

    public InMemoryDomainEventDispatcher(IServiceProvider provider) => _provider = provider;

    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = _provider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler is null) continue;
                var method = handlerType.GetMethod("HandleAsync")
                    ?? throw new InvalidOperationException($"HandleAsync saknas på {handlerType}.");
                var task = (Task?)method.Invoke(handler, [domainEvent, cancellationToken])
                    ?? throw new InvalidOperationException($"HandleAsync returnerade null på {handler.GetType()}.");
                await task;
            }
        }
    }
}
