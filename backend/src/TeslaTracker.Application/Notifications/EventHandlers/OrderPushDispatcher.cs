using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Notifications.Ports;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;

namespace TeslaTracker.Application.Notifications.EventHandlers;

public sealed class OrderPushDispatcher :
    IDomainEventHandler<VinAssigned>,
    IDomainEventHandler<DeliveryWindowChanged>,
    IDomainEventHandler<OrderStateChanged>
{
    private readonly IPushChannelRepository _channels;
    private readonly IPushNotifier _notifier;

    public OrderPushDispatcher(IPushChannelRepository channels, IPushNotifier notifier)
    {
        _channels = channels;
        _notifier = notifier;
    }

    public Task HandleAsync(VinAssigned domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var payload = new PushPayload(
            "Tesla-uppdatering",
            $"VIN tilldelad: {domainEvent.Vin.Value}",
            $"/track/{domainEvent.OrderId.Value}");
        return FanOutAsync(domainEvent.OrderId, payload, cancellationToken);
    }

    public Task HandleAsync(DeliveryWindowChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var to = domainEvent.NewWindow.DisplayText;
        var payload = new PushPayload(
            "Tesla-uppdatering",
            string.IsNullOrWhiteSpace(to)
                ? "Leveransfönstret har uppdaterats."
                : $"Nytt leveransfönster: {to}",
            $"/track/{domainEvent.OrderId.Value}");
        return FanOutAsync(domainEvent.OrderId, payload, cancellationToken);
    }

    public Task HandleAsync(OrderStateChanged domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var message = domainEvent.NewState switch
        {
            OrderState.InProduction => "Bilen är nu i produktion.",
            OrderState.Built => "Bilen är färdigbyggd.",
            OrderState.InTransit => "Bilen är på väg.",
            OrderState.ReadyForDelivery => "Bilen är redo för leverans!",
            OrderState.Delivered => "Bilen är levererad. Grattis!",
            _ => $"Status uppdaterad: {domainEvent.NewState}",
        };
        var payload = new PushPayload("Tesla-uppdatering", message, $"/track/{domainEvent.OrderId.Value}");
        return FanOutAsync(domainEvent.OrderId, payload, cancellationToken);
    }

    private async Task FanOutAsync(OrderId orderId, PushPayload payload, CancellationToken cancellationToken)
    {
        var dead = new List<PushChannel>();
        var failing = new List<PushChannel>();

        await foreach (var channel in _channels.FindByOrderAsync(orderId, cancellationToken))
        {
            var status = await _notifier.SendAsync(channel, payload, cancellationToken);
            switch (status)
            {
                case PushDeliveryStatus.Delivered:
                    channel.RecordSuccess();
                    break;
                case PushDeliveryStatus.Gone:
                    dead.Add(channel);
                    break;
                case PushDeliveryStatus.Failed:
                    if (channel.RecordFailure())
                    {
                        dead.Add(channel);
                    }
                    else
                    {
                        failing.Add(channel);
                    }
                    break;
            }
        }

        foreach (var channel in dead)
        {
            await _channels.RemoveAsync(channel, cancellationToken);
        }

        foreach (var channel in failing)
        {
            await _channels.UpdateAsync(channel, cancellationToken);
        }
    }
}
