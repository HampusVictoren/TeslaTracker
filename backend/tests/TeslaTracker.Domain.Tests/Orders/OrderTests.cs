using FluentAssertions;
using TeslaTracker.Domain.DomainExceptions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using Xunit;

namespace TeslaTracker.Domain.Tests.Orders;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static OrderId AnOrderId() => OrderId.Create("RN123456789").Value;
    private static Vin AVin() => Vin.Create("5YJYGDEE0LF000001").Value;

    private static TrackingSecret ASecret() =>
        TrackingSecret.Create(new byte[] { 0x01, 0x02, 0x03 }, "kv-key-1").Value;

    private static OrderSnapshot ASnapshot(
        Vin? vin = null,
        DeliveryWindow? window = null,
        OrderState state = OrderState.OrderPlaced,
        string hash = "h1") =>
        new(vin, window ?? DeliveryWindow.Unknown(), "Model Y", state, hash);

    [Fact]
    public void Register_Creates_Active_Order_With_No_Pending_Events()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);

        order.IsActive.Should().BeTrue();
        order.ConsecutiveFailures.Should().Be(0);
        order.LastSyncedAt.Should().Be(Now);
        order.CreatedAt.Should().Be(Now);
        order.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplySnapshot_With_Same_Hash_Resets_Failures_Without_Events()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(hash: "h1"), Now);
        order.RecordSyncFailure(Now);
        order.ClearPendingEvents();

        order.ApplySnapshot(ASnapshot(hash: "h1"), Now.AddHours(1));

        order.PendingEvents.Should().BeEmpty();
        order.ConsecutiveFailures.Should().Be(0);
        order.LastSyncedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void ApplySnapshot_With_New_Vin_Raises_VinAssigned()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(hash: "h1"), Now);

        order.ApplySnapshot(ASnapshot(vin: AVin(), hash: "h2"), Now.AddHours(1));

        order.PendingEvents.Should().ContainSingle(e => e is VinAssigned);
        var assigned = (VinAssigned)order.PendingEvents.Single(e => e is VinAssigned);
        assigned.Vin.Should().Be(AVin());
        assigned.OccurredAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void ApplySnapshot_With_New_Delivery_Window_Raises_DeliveryWindowChanged()
    {
        var initialWindow = DeliveryWindow.Create(null, null, "TBD").Value;
        var newWindow = DeliveryWindow.Create(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "Juli 2026").Value;

        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(window: initialWindow, hash: "h1"), Now);

        order.ApplySnapshot(ASnapshot(window: newWindow, hash: "h2"), Now.AddHours(1));

        var changed = order.PendingEvents.OfType<DeliveryWindowChanged>().Single();
        changed.PreviousWindow.Should().Be(initialWindow);
        changed.NewWindow.Should().Be(newWindow);
    }

    [Fact]
    public void ApplySnapshot_With_New_State_Raises_OrderStateChanged()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(state: OrderState.OrderPlaced, hash: "h1"), Now);

        order.ApplySnapshot(ASnapshot(state: OrderState.InProduction, hash: "h2"), Now.AddHours(1));

        var stateChanged = order.PendingEvents.OfType<OrderStateChanged>().Single();
        stateChanged.PreviousState.Should().Be(OrderState.OrderPlaced);
        stateChanged.NewState.Should().Be(OrderState.InProduction);
    }

    [Fact]
    public void ApplySnapshot_To_Delivered_Archives_Order_With_Completed_Reason()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(state: OrderState.InTransit, hash: "h1"), Now);

        order.ApplySnapshot(ASnapshot(state: OrderState.Delivered, hash: "h2"), Now.AddHours(1));

        order.IsActive.Should().BeFalse();
        var archived = order.PendingEvents.OfType<OrderArchived>().Single();
        archived.Reason.Should().Be(ArchiveReason.Completed);
    }

    [Fact]
    public void ApplySnapshot_On_Archived_Order_Throws_InvariantViolation()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);
        order.Stop(Now);

        var act = () => order.ApplySnapshot(ASnapshot(hash: "different"), Now.AddHours(1));

        act.Should().Throw<InvariantViolationException>();
    }

    [Fact]
    public void RecordSyncFailure_Increments_Counter()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);

        order.RecordSyncFailure(Now);
        order.RecordSyncFailure(Now);

        order.ConsecutiveFailures.Should().Be(2);
        order.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordSyncFailure_Beyond_Max_Archives_Order()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);

        for (var i = 0; i <= Order.MaxConsecutiveFailures; i++)
        {
            order.RecordSyncFailure(Now);
        }

        order.IsActive.Should().BeFalse();
        var archived = order.PendingEvents.OfType<OrderArchived>().Single();
        archived.Reason.Should().Be(ArchiveReason.MaxFailuresExceeded);
    }

    [Fact]
    public void Stop_On_Active_Order_Archives_With_UserRequested()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);

        order.Stop(Now);

        order.IsActive.Should().BeFalse();
        order.PendingEvents.OfType<OrderArchived>().Single().Reason.Should().Be(ArchiveReason.UserRequested);
    }

    [Fact]
    public void Stop_On_Already_Archived_Order_Is_NoOp()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);
        order.Stop(Now);
        order.ClearPendingEvents();

        order.Stop(Now.AddHours(1));

        order.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkTokenRevoked_Archives_With_TokenRevoked_Reason()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);

        order.MarkTokenRevoked(Now);

        order.IsActive.Should().BeFalse();
        order.PendingEvents.OfType<OrderArchived>().Single().Reason.Should().Be(ArchiveReason.TokenRevoked);
    }

    [Fact]
    public void Reactivate_On_Archived_Order_Restores_State()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(hash: "old"), Now);
        order.Stop(Now);
        order.ClearPendingEvents();

        var freshSecret = TrackingSecret.Create(new byte[] { 9, 9, 9 }, "kv-key-2").Value;
        var freshSnapshot = ASnapshot(hash: "new");

        order.Reactivate(freshSecret, freshSnapshot, Now.AddDays(1));

        order.IsActive.Should().BeTrue();
        order.Secret.Should().Be(freshSecret);
        order.CurrentSnapshot.Should().Be(freshSnapshot);
        order.LastSyncedAt.Should().Be(Now.AddDays(1));
        order.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void Reactivate_On_Active_Order_Throws()
    {
        var order = Order.Register(AnOrderId(), ASecret(), ASnapshot(), Now);
        var act = () => order.Reactivate(ASecret(), ASnapshot(), Now);

        act.Should().Throw<InvariantViolationException>();
    }
}
