namespace TeslaTracker.Application.Notifications.Commands.AttachPushChannel;

public sealed record AttachPushChannelCommand(
    string OrderId,
    string PresentedViewToken,
    string Endpoint,
    string P256dh,
    string Auth,
    string UserAgent);
