using Microsoft.Extensions.Logging;
using TeslaTracker.Application.Notifications.Ports;
using TeslaTracker.Domain.Notifications;

namespace TeslaTracker.Infrastructure.Notifications;

internal sealed class LoggingPushNotifier : IPushNotifier
{
    private readonly ILogger<LoggingPushNotifier> _logger;

    public LoggingPushNotifier(ILogger<LoggingPushNotifier> logger) => _logger = logger;

    public Task<PushDeliveryStatus> SendAsync(PushChannel channel, PushPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        _logger.LogInformation(
            "LoggingPushNotifier (DEV) → order {OrderId}, endpoint-hash {EndpointHash}: {Title} / {Body} → {Url}",
            channel.OrderId.Value,
            channel.EndpointHash,
            payload.Title,
            payload.Body,
            payload.Url ?? "(no url)");

        return Task.FromResult(PushDeliveryStatus.Delivered);
    }
}
