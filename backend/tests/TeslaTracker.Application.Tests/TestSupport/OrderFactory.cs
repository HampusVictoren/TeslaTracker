using TeslaTracker.Domain.Orders;

namespace TeslaTracker.Application.Tests.TestSupport;

internal static class OrderFactory
{
    public static OrderId AnOrderId(string raw = "RN123456789") => OrderId.Create(raw).Value;
    public static Vin AVin(string raw = "5YJYGDEE0LF000001") => Vin.Create(raw).Value;

    public static TrackingSecret ASecret(string keyId = "kv-key-1") =>
        TrackingSecret.Create(new byte[] { 0x01, 0x02, 0x03 }, keyId).Value;

    public static ViewToken AViewToken() => ViewToken.Issue().Token;

    public static OrderSnapshot ASnapshot(
        Vin? vin = null,
        OrderState state = OrderState.OrderPlaced,
        string hash = "h1") =>
        new(vin, DeliveryWindow.Unknown(), "Model Y", state, hash);
}
