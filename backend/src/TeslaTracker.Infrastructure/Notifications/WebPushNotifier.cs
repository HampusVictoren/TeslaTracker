using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeslaTracker.Application.Notifications.Ports;
using TeslaTracker.Domain.Notifications;
using WebPush;

namespace TeslaTracker.Infrastructure.Notifications;

internal sealed class WebPushNotifier : IPushNotifier, IDisposable
{
    private readonly WebPushClient _client;
    private readonly VapidDetails _vapid;
    private readonly ILogger<WebPushNotifier> _logger;

    public void Dispose() => _client.Dispose();

    public WebPushNotifier(IOptions<NotificationsOptions> options, ILogger<WebPushNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var o = options.Value;
        if (!o.IsConfigured)
        {
            throw new InvalidOperationException(
                "WebPushNotifier kräver Notifications:VapidSubject, VapidPublicKey och VapidPrivateKey.");
        }

        _vapid = new VapidDetails(o.VapidSubject, o.VapidPublicKey, o.VapidPrivateKey);
        _client = new WebPushClient();
        _logger = logger;
    }

    public async Task<PushDeliveryStatus> SendAsync(PushChannel channel, PushPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(payload);

        var subscription = new PushSubscription(channel.Endpoint.Url, channel.Endpoint.P256dh, channel.Endpoint.Auth);
        var body = JsonSerializer.Serialize(new
        {
            title = payload.Title,
            body = payload.Body,
            url = payload.Url,
        });

        try
        {
            await _client.SendNotificationAsync(subscription, body, _vapid, cancellationToken);
            return PushDeliveryStatus.Delivered;
        }
        catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Push endpoint {Hash} is gone ({Status}); will be removed.", channel.EndpointHash, ex.StatusCode);
            return PushDeliveryStatus.Gone;
        }
        catch (WebPushException ex)
        {
            _logger.LogWarning("WebPush failure for {Hash}: {Status} {Message}", channel.EndpointHash, ex.StatusCode, ex.Message);
            return PushDeliveryStatus.Failed;
        }
    }
}
