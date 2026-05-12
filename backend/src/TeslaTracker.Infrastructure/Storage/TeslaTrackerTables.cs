namespace TeslaTracker.Infrastructure.Storage;

public static class TeslaTrackerTables
{
    public const string Orders = "orders";
    public const string OrderEventHistory = "orderhistory";
    public const string PushChannels = "pushchannels";
    public const string RateLimits = "ratelimit";

    public static IReadOnlyList<string> All { get; } =
        [Orders, OrderEventHistory, PushChannels, RateLimits];
}
