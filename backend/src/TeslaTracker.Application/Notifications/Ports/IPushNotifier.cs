using TeslaTracker.Domain.Notifications;

namespace TeslaTracker.Application.Notifications.Ports;

public sealed record PushPayload(string Title, string Body, string? Url = null);

public enum PushDeliveryStatus
{
    Delivered,
    Failed,
    Gone,
}

public interface IPushNotifier
{
    Task<PushDeliveryStatus> SendAsync(PushChannel channel, PushPayload payload, CancellationToken cancellationToken);
}
