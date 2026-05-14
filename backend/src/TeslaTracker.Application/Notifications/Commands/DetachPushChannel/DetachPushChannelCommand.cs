namespace TeslaTracker.Application.Notifications.Commands.DetachPushChannel;

public sealed record DetachPushChannelCommand(
    string OrderId,
    string PresentedViewToken,
    string EndpointHash);
