using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders.Events;

namespace TeslaTracker.Application.Notifications.EventHandlers;

/// <summary>
/// On order archival (Stop / TokenRevoked / MaxFailures), remove every push channel
/// associated with the order. GDPR-friendly: deleting "tracking" also deletes the
/// channel data the user provided. Skips push notification — there is nothing left
/// to notify since the user already triggered the archival or their token is dead.
/// </summary>
public sealed class OrderArchivedPushCleanup : IDomainEventHandler<OrderArchived>
{
    private readonly IPushChannelRepository _channels;

    public OrderArchivedPushCleanup(IPushChannelRepository channels) => _channels = channels;

    public async Task HandleAsync(OrderArchived domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var orderId = domainEvent.OrderId;
        await foreach (var channel in _channels.FindByOrderAsync(orderId, cancellationToken))
        {
            await _channels.RemoveAsync(channel, cancellationToken);
        }
    }
}
