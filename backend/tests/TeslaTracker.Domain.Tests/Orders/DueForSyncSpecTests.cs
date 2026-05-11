using FluentAssertions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Specifications;
using Xunit;

namespace TeslaTracker.Domain.Tests.Orders;

public class DueForSyncSpecTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static Order ActiveOrderSyncedAt(DateTimeOffset syncedAt)
    {
        var id = OrderId.Create("RN123456789").Value;
        var secret = TrackingSecret.Create(new byte[] { 1, 2, 3 }, "kv-key").Value;
        var snapshot = new OrderSnapshot(null, DeliveryWindow.Unknown(), "Model Y", OrderState.OrderPlaced, "h1");
        return Order.Rehydrate(id, secret, snapshot, syncedAt, 0, isActive: true, syncedAt);
    }

    [Fact]
    public void Order_Synced_Less_Than_50_Minutes_Ago_Is_Not_Due()
    {
        var order = ActiveOrderSyncedAt(Now.AddMinutes(-30));
        var spec = new DueForSyncSpec(Now);

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void Order_Synced_Exactly_50_Minutes_Ago_Is_Due()
    {
        var order = ActiveOrderSyncedAt(Now - DueForSyncSpec.SyncInterval);
        var spec = new DueForSyncSpec(Now);

        spec.IsSatisfiedBy(order).Should().BeTrue();
    }

    [Fact]
    public void Order_Synced_Long_Ago_Is_Due()
    {
        var order = ActiveOrderSyncedAt(Now.AddHours(-3));
        var spec = new DueForSyncSpec(Now);

        spec.IsSatisfiedBy(order).Should().BeTrue();
    }

    [Fact]
    public void Archived_Order_Is_Never_Due()
    {
        var order = ActiveOrderSyncedAt(Now.AddHours(-10));
        order.Stop(Now);
        var spec = new DueForSyncSpec(Now);

        spec.IsSatisfiedBy(order).Should().BeFalse();
    }
}
