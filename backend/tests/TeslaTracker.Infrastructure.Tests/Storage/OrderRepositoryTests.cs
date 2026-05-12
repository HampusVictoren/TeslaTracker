using FluentAssertions;
using NSubstitute;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage;
using TeslaTracker.Infrastructure.Tests.TestSupport;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.Storage;

[Collection("Azurite")]
public class OrderRepositoryTests : IClassFixture<AzuriteFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly AzuriteFixture _fixture;

    public OrderRepositoryTests(AzuriteFixture fixture) => _fixture = fixture;

    private OrderRepository CreateRepository()
    {
        var tracker = Substitute.For<IAggregateTracker>();
        return new OrderRepository(_fixture.ServiceClient, tracker);
    }

    private static Order ActiveOrder(string orderId = "RN111111111")
    {
        var id = OrderId.Create(orderId).Value;
        var secret = TrackingSecret.Create(new byte[] { 0xAA, 0xBB }, "kv-key").Value;
        var snapshot = new OrderSnapshot(null, DeliveryWindow.Unknown(), "M3", OrderState.OrderPlaced, "h1");
        return Order.Register(id, secret, snapshot, Now);
    }

    [RequiresAzuriteFact]
    public async Task Add_Then_Find_Roundtrips_Order()
    {
        var repo = CreateRepository();
        var order = ActiveOrder("RN222222222");

        await repo.AddAsync(order, CancellationToken.None);
        var found = await repo.FindAsync(order.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(order.Id);
        found.IsActive.Should().BeTrue();
        found.CurrentSnapshot.RawHash.Should().Be("h1");
    }

    [RequiresAzuriteFact]
    public async Task Find_Returns_Null_When_Order_Missing()
    {
        var repo = CreateRepository();

        var found = await repo.FindAsync(OrderId.Create("RN333333333").Value, CancellationToken.None);

        found.Should().BeNull();
    }

    [RequiresAzuriteFact]
    public async Task Update_Changes_State_And_Increments_Failures()
    {
        var repo = CreateRepository();
        var order = ActiveOrder("RN444444444");
        await repo.AddAsync(order, CancellationToken.None);

        var loaded = (await repo.FindAsync(order.Id, CancellationToken.None))!;
        loaded.RecordSyncFailure(Now.AddMinutes(5));
        await repo.UpdateAsync(loaded, CancellationToken.None);

        var freshlyLoaded = (await repo.FindAsync(order.Id, CancellationToken.None))!;
        freshlyLoaded.ConsecutiveFailures.Should().Be(1);
    }

    [RequiresAzuriteFact]
    public async Task Stopping_Order_Moves_Row_To_Archived_Partition()
    {
        var repo = CreateRepository();
        var order = ActiveOrder("RN555555555");
        await repo.AddAsync(order, CancellationToken.None);

        var loaded = (await repo.FindAsync(order.Id, CancellationToken.None))!;
        loaded.Stop(Now.AddHours(1));
        await repo.UpdateAsync(loaded, CancellationToken.None);

        var found = await repo.FindAsync(order.Id, CancellationToken.None);
        found.Should().NotBeNull();
        found!.IsActive.Should().BeFalse();
    }

    [RequiresAzuriteFact]
    public async Task FindActiveDueForSync_Returns_Only_Active_Older_Than_Threshold()
    {
        var repo = CreateRepository();

        var due = ActiveOrder("RN666666666");
        await repo.AddAsync(due, CancellationToken.None);

        var fresh = ActiveOrder("RN777777777");
        await repo.AddAsync(fresh, CancellationToken.None);
        var freshLoaded = (await repo.FindAsync(fresh.Id, CancellationToken.None))!;
        freshLoaded.ApplySnapshot(new OrderSnapshot(null, DeliveryWindow.Unknown(), "M3", OrderState.OrderPlaced, "h-fresh"), Now.AddHours(2));
        await repo.UpdateAsync(freshLoaded, CancellationToken.None);

        var threshold = Now.AddHours(1);
        var dueOrders = new List<Order>();
        await foreach (var o in repo.FindActiveDueForSyncAsync(threshold, CancellationToken.None))
        {
            dueOrders.Add(o);
        }

        dueOrders.Select(o => o.Id.Value).Should().Contain("RN666666666");
        dueOrders.Select(o => o.Id.Value).Should().NotContain("RN777777777");
    }
}
