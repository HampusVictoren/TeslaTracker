using FluentAssertions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage;
using TeslaTracker.Infrastructure.Storage.Mappers;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.Storage;

public class OrderMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static Order ActiveOrder()
    {
        var id = OrderId.Create("RN123456789").Value;
        var secret = TrackingSecret.Create(new byte[] { 1, 2, 3, 4, 5 }, "kv-key-1").Value;
        var vin = Vin.Create("5YJYGDEE0LF000001").Value;
        var window = DeliveryWindow.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), "Juni 2026").Value;
        var snapshot = new OrderSnapshot(vin, window, "Model Y", OrderState.InProduction, "h-original");
        return Order.Register(id, secret, snapshot, Now);
    }

    [Fact]
    public void Roundtrip_Preserves_All_Fields()
    {
        var order = ActiveOrder();

        var entity = OrderMapper.ToEntity(order);
        var rehydrated = OrderMapper.ToDomain(entity);

        rehydrated.Id.Should().Be(order.Id);
        rehydrated.Secret.KeyId.Should().Be(order.Secret.KeyId);
        rehydrated.Secret.Cipher.ToArray().Should().Equal(order.Secret.Cipher.ToArray());
        rehydrated.LastSyncedAt.Should().Be(order.LastSyncedAt);
        rehydrated.ConsecutiveFailures.Should().Be(order.ConsecutiveFailures);
        rehydrated.IsActive.Should().Be(order.IsActive);
        rehydrated.CreatedAt.Should().Be(order.CreatedAt);
    }

    [Fact]
    public void Snapshot_Roundtrip_Preserves_Hash()
    {
        var order = ActiveOrder();

        var entity = OrderMapper.ToEntity(order);
        var rehydrated = OrderMapper.ToDomain(entity);

        rehydrated.CurrentSnapshot.RawHash.Should().Be(order.CurrentSnapshot.RawHash);
        rehydrated.CurrentSnapshot.Should().Be(order.CurrentSnapshot);
    }

    [Fact]
    public void Active_Order_Has_ACTIVE_PartitionKey()
    {
        var order = ActiveOrder();

        var entity = OrderMapper.ToEntity(order);

        entity.PartitionKey.Should().Be(PartitionKeys.Active);
    }

    [Fact]
    public void Archived_Order_Has_ARCHIVED_PartitionKey()
    {
        var order = ActiveOrder();
        order.Stop(Now);

        var entity = OrderMapper.ToEntity(order);

        entity.PartitionKey.Should().Be(PartitionKeys.Archived);
    }

    [Fact]
    public void Snapshot_Without_Vin_Roundtrips_To_Null_Vin()
    {
        var id = OrderId.Create("RN123456789").Value;
        var secret = TrackingSecret.Create(new byte[] { 9, 9 }, "k").Value;
        var snapshot = new OrderSnapshot(null, DeliveryWindow.Unknown(), "M3", OrderState.OrderPlaced, "h");
        var order = Order.Register(id, secret, snapshot, Now);

        var rehydrated = OrderMapper.ToDomain(OrderMapper.ToEntity(order));

        rehydrated.CurrentSnapshot.Vin.Should().BeNull();
    }
}
