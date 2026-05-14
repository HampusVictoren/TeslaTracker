using NSubstitute;
using TeslaTracker.Application.Notifications.EventHandlers;
using TeslaTracker.Application.Tests.TestSupport;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using Xunit;

namespace TeslaTracker.Application.Tests.Notifications;

public class OrderArchivedPushCleanupTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static PushChannel ChannelFor(OrderId orderId, string url) =>
        PushChannel.Attach(orderId, PushEndpoint.Create(url, "p256", "auth").Value, "UA", Now);

    private static async IAsyncEnumerable<PushChannel> AsAsync(IEnumerable<PushChannel> source)
    {
        foreach (var c in source) { yield return c; await Task.Yield(); }
    }

    [Fact]
    public async Task Removes_Every_Channel_For_Archived_Order()
    {
        var orderId = OrderFactory.AnOrderId();
        var ch1 = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/a");
        var ch2 = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/b");
        var ch3 = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/c");
        var channels = Substitute.For<IPushChannelRepository>();
        channels.FindByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(AsAsync(new[] { ch1, ch2, ch3 }));

        var cleanup = new OrderArchivedPushCleanup(channels);
        await cleanup.HandleAsync(new OrderArchived(orderId, ArchiveReason.UserRequested, Now), CancellationToken.None);

        await channels.Received(1).RemoveAsync(ch1, Arg.Any<CancellationToken>());
        await channels.Received(1).RemoveAsync(ch2, Arg.Any<CancellationToken>());
        await channels.Received(1).RemoveAsync(ch3, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ArchiveReason.UserRequested)]
    [InlineData(ArchiveReason.TokenRevoked)]
    [InlineData(ArchiveReason.MaxFailuresExceeded)]
    [InlineData(ArchiveReason.Completed)]
    public async Task Cleanup_Runs_Regardless_Of_Archive_Reason(ArchiveReason reason)
    {
        var orderId = OrderFactory.AnOrderId();
        var channel = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/x");
        var channels = Substitute.For<IPushChannelRepository>();
        channels.FindByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(AsAsync(new[] { channel }));

        var cleanup = new OrderArchivedPushCleanup(channels);
        await cleanup.HandleAsync(new OrderArchived(orderId, reason, Now), CancellationToken.None);

        await channels.Received(1).RemoveAsync(channel, Arg.Any<CancellationToken>());
    }
}
