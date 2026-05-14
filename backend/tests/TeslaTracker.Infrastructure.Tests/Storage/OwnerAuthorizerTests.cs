using FluentAssertions;
using NSubstitute;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.Storage;

public class OwnerAuthorizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static (Order Order, string Plaintext) CreateOrder(string orderId = "RN111111111")
    {
        var id = OrderId.Create(orderId).Value;
        var secret = TrackingSecret.Create(new byte[] { 1 }, "kv").Value;
        var (viewToken, plaintext) = ViewToken.Issue();
        var snapshot = new OrderSnapshot(null, DeliveryWindow.Unknown(), "MY", OrderState.OrderPlaced, "h");
        return (Order.Register(id, secret, viewToken, snapshot, Now), plaintext);
    }

    [Fact]
    public async Task Returns_Unauthorized_When_Token_Is_Empty()
    {
        var repo = Substitute.For<IOrderRepository>();
        var authorizer = new OwnerAuthorizer(repo);

        var result = await authorizer.AuthorizeAsync("RN111111111", "", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Returns_Unauthorized_When_OrderId_Is_Malformed()
    {
        var repo = Substitute.For<IOrderRepository>();
        var authorizer = new OwnerAuthorizer(repo);

        var result = await authorizer.AuthorizeAsync("not-an-rn", "any-token", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
        await repo.DidNotReceive().FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_Unauthorized_When_Order_Does_Not_Exist()
    {
        var repo = Substitute.For<IOrderRepository>();
        repo.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns((Order?)null);
        var authorizer = new OwnerAuthorizer(repo);

        var result = await authorizer.AuthorizeAsync("RN999999999", "any-token", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Returns_Unauthorized_When_Token_Does_Not_Match_Order()
    {
        var (order, _) = CreateOrder();
        var repo = Substitute.For<IOrderRepository>();
        repo.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);
        var authorizer = new OwnerAuthorizer(repo);

        var result = await authorizer.AuthorizeAsync("RN111111111", "wrong-plaintext", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Returns_Order_When_Token_Matches()
    {
        var (order, plaintext) = CreateOrder();
        var repo = Substitute.For<IOrderRepository>();
        repo.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);
        var authorizer = new OwnerAuthorizer(repo);

        var result = await authorizer.AuthorizeAsync("RN111111111", plaintext, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Value.Should().Be("RN111111111");
    }

    [Fact]
    public async Task Returns_Same_Error_Code_For_All_Failure_Causes()
    {
        // Defense: ensure no enumeration of order existence is possible via error code differences.
        var repo = Substitute.For<IOrderRepository>();
        repo.FindAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns((Order?)null);
        var authorizer = new OwnerAuthorizer(repo);

        var emptyToken = await authorizer.AuthorizeAsync("RN111111111", "", CancellationToken.None);
        var malformedId = await authorizer.AuthorizeAsync("bad-id", "token", CancellationToken.None);
        var unknownOrder = await authorizer.AuthorizeAsync("RN999999999", "token", CancellationToken.None);

        emptyToken.Error.Code.Should().Be(malformedId.Error.Code);
        malformedId.Error.Code.Should().Be(unknownOrder.Error.Code);
    }
}
