namespace TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;

public sealed record RegisterOrderTrackingCommand(
    string OrderId,
    string RefreshToken,
    string ClientIpAddress);
