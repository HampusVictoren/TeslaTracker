using FluentAssertions;
using NSubstitute;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Orders.Commands.SyncOrderWithTesla;
using TeslaTracker.Application.Tesla;
using TeslaTracker.Application.Tests.TestSupport;
using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using TeslaTracker.Domain.SeedWork;
using Xunit;

namespace TeslaTracker.Application.Tests.Orders;

public class SyncOrderWithTeslaHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ITeslaOrderGateway _tesla = Substitute.For<ITeslaOrderGateway>();
    private readonly ITokenProtector _tokenProtector = Substitute.For<ITokenProtector>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeClock _clock = new();

    private SyncOrderWithTeslaHandler CreateHandler() =>
        new(_orders, _tesla, _tokenProtector, _unitOfWork, _clock);

    public SyncOrderWithTeslaHandlerTests()
    {
        _tokenProtector.UnprotectAsync(Arg.Any<TrackingSecret>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("plaintext-refresh-token"));
        _tokenProtector.ProtectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OrderFactory.ASecret(keyId: "kv-key-rotated"));
    }

    [Fact]
    public async Task Returns_Failure_When_Order_Not_Found()
    {
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var result = await CreateHandler().HandleAsync(new SyncOrderWithTeslaCommand("RN123456789"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sync.OrderNotFound");
    }

    [Fact]
    public async Task Marks_Token_Revoked_When_Tesla_Returns_Unauthorized()
    {
        var order = Order.Register(OrderFactory.AnOrderId(), OrderFactory.ASecret(), OrderFactory.AViewToken(), OrderFactory.ASnapshot(), _clock.UtcNow);
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);
        _tesla.FetchOrderAsync(Arg.Any<OrderId>(), Arg.Any<TeslaCredential>(), Arg.Any<CancellationToken>())
            .Returns(Result<TeslaSyncResult>.Failure("Tesla.Unauthorized", "Token expired."));

        var result = await CreateHandler().HandleAsync(new SyncOrderWithTeslaCommand("RN123456789"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        order.IsActive.Should().BeFalse();
        order.PendingEvents.OfType<OrderArchived>().Single().Reason.Should().Be(ArchiveReason.TokenRevoked);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Records_Sync_Failure_On_Transient_Tesla_Error()
    {
        var order = Order.Register(OrderFactory.AnOrderId(), OrderFactory.ASecret(), OrderFactory.AViewToken(), OrderFactory.ASnapshot(), _clock.UtcNow);
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);
        _tesla.FetchOrderAsync(Arg.Any<OrderId>(), Arg.Any<TeslaCredential>(), Arg.Any<CancellationToken>())
            .Returns(Result<TeslaSyncResult>.Failure("Tesla.Unavailable", "Service unavailable."));

        await CreateHandler().HandleAsync(new SyncOrderWithTeslaCommand("RN123456789"), CancellationToken.None);

        order.ConsecutiveFailures.Should().Be(1);
        order.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Applies_New_Snapshot_And_Rotates_Secret_On_Success()
    {
        var order = Order.Register(OrderFactory.AnOrderId(), OrderFactory.ASecret(), OrderFactory.AViewToken(), OrderFactory.ASnapshot(hash: "h1"), _clock.UtcNow);
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);

        var newSnapshot = OrderFactory.ASnapshot(vin: OrderFactory.AVin(), hash: "h2");
        _tesla.FetchOrderAsync(Arg.Any<OrderId>(), Arg.Any<TeslaCredential>(), Arg.Any<CancellationToken>())
            .Returns(Result<TeslaSyncResult>.Success(new TeslaSyncResult(newSnapshot, "new-refresh-token")));

        _clock.UtcNow = _clock.UtcNow.AddHours(1);

        var result = await CreateHandler().HandleAsync(new SyncOrderWithTeslaCommand("RN123456789"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.CurrentSnapshot.Should().Be(newSnapshot);
        order.PendingEvents.OfType<VinAssigned>().Should().HaveCount(1);
        await _tokenProtector.Received(1).ProtectAsync("new-refresh-token", Arg.Any<CancellationToken>());
    }
}
