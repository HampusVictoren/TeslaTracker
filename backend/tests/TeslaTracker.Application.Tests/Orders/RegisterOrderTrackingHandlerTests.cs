using FluentAssertions;
using NSubstitute;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;
using TeslaTracker.Application.Tesla;
using TeslaTracker.Application.Tests.TestSupport;
using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;
using Xunit;

namespace TeslaTracker.Application.Tests.Orders;

public class RegisterOrderTrackingHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ITeslaOrderGateway _tesla = Substitute.For<ITeslaOrderGateway>();
    private readonly ITokenProtector _tokenProtector = Substitute.For<ITokenProtector>();
    private readonly IRateLimiter _rateLimiter = Substitute.For<IRateLimiter>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeClock _clock = new();

    private RegisterOrderTrackingHandler CreateHandler() =>
        new(_orders, _tesla, _tokenProtector, _rateLimiter, _unitOfWork, _clock);

    public RegisterOrderTrackingHandlerTests()
    {
        _rateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _tokenProtector.ProtectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OrderFactory.ASecret());
    }

    [Fact]
    public async Task Returns_Failure_When_OrderId_Is_Invalid()
    {
        var command = new RegisterOrderTrackingCommand("not-an-rn", "token", "1.2.3.4");

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OrderId.InvalidFormat");
        await _tesla.DidNotReceive().FetchOrderAsync(Arg.Any<OrderId>(), Arg.Any<TeslaCredential>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_Failure_When_RefreshToken_Is_Empty()
    {
        var command = new RegisterOrderTrackingCommand("RN123456789", "  ", "1.2.3.4");

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Registration.MissingToken");
    }

    [Fact]
    public async Task Returns_Failure_When_Rate_Limit_Exceeded()
    {
        _rateLimiter.TryAcquireAsync("ip:1.2.3.4", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);
        var command = new RegisterOrderTrackingCommand("RN123456789", "token", "1.2.3.4");

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Registration.RateLimited");
    }

    [Fact]
    public async Task Returns_Failure_When_Order_Already_Actively_Tracked()
    {
        var existingOrder = Order.Register(OrderFactory.AnOrderId(), OrderFactory.ASecret(), OrderFactory.ASnapshot(), _clock.UtcNow);
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(existingOrder);

        var command = new RegisterOrderTrackingCommand("RN123456789", "token", "1.2.3.4");
        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Registration.AlreadyTracked");
    }

    [Fact]
    public async Task Bubbles_Up_Tesla_Failure_Without_Saving()
    {
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns((Order?)null);
        _tesla.FetchOrderAsync(Arg.Any<OrderId>(), Arg.Any<TeslaCredential>(), Arg.Any<CancellationToken>())
            .Returns(Result<TeslaSyncResult>.Failure("Tesla.Unauthorized", "Invalid refresh token."));

        var result = await CreateHandler().HandleAsync(
            new RegisterOrderTrackingCommand("RN123456789", "token", "1.2.3.4"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tesla.Unauthorized");
        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registers_New_Order_When_Tesla_Returns_Snapshot()
    {
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns((Order?)null);
        var snapshot = OrderFactory.ASnapshot();
        _tesla.FetchOrderAsync(Arg.Any<OrderId>(), Arg.Any<TeslaCredential>(), Arg.Any<CancellationToken>())
            .Returns(Result<TeslaSyncResult>.Success(new TeslaSyncResult(snapshot, "rotated-token")));

        var result = await CreateHandler().HandleAsync(
            new RegisterOrderTrackingCommand("RN123456789", "token", "1.2.3.4"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("RN123456789");

        await _tokenProtector.Received(1).ProtectAsync("rotated-token", Arg.Any<CancellationToken>());
        await _orders.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reactivates_Archived_Order_On_Repeat_Registration()
    {
        var archived = Order.Register(OrderFactory.AnOrderId(), OrderFactory.ASecret(), OrderFactory.ASnapshot(), _clock.UtcNow);
        archived.Stop(_clock.UtcNow);
        archived.ClearPendingEvents();

        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(archived);

        _tesla.FetchOrderAsync(Arg.Any<OrderId>(), Arg.Any<TeslaCredential>(), Arg.Any<CancellationToken>())
            .Returns(Result<TeslaSyncResult>.Success(new TeslaSyncResult(OrderFactory.ASnapshot(hash: "different"), "rotated-token")));

        var result = await CreateHandler().HandleAsync(
            new RegisterOrderTrackingCommand("RN123456789", "token", "1.2.3.4"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        archived.IsActive.Should().BeTrue();
        await _orders.Received(1).UpdateAsync(archived, Arg.Any<CancellationToken>());
        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }
}
