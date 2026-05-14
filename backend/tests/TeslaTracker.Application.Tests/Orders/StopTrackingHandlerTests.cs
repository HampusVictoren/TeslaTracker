using FluentAssertions;
using NSubstitute;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Orders.Commands.StopTracking;
using TeslaTracker.Application.Tests.TestSupport;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using Xunit;

namespace TeslaTracker.Application.Tests.Orders;

public class StopTrackingHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeClock _clock = new();

    private StopTrackingHandler CreateHandler() => new(_orders, _unitOfWork, _clock);

    [Fact]
    public async Task Returns_Failure_When_Order_Not_Found()
    {
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var result = await CreateHandler().HandleAsync(new StopTrackingCommand("RN123456789"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StopTracking.NotFound");
    }

    [Fact]
    public async Task Archives_Active_Order_With_UserRequested_Reason()
    {
        var order = Order.Register(OrderFactory.AnOrderId(), OrderFactory.ASecret(), OrderFactory.AViewToken(), OrderFactory.ASnapshot(), _clock.UtcNow);
        _orders.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);

        var result = await CreateHandler().HandleAsync(new StopTrackingCommand("RN123456789"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.IsActive.Should().BeFalse();
        order.PendingEvents.OfType<OrderArchived>().Single().Reason.Should().Be(ArchiveReason.UserRequested);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
