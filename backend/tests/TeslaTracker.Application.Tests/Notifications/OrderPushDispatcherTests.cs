using FluentAssertions;
using NSubstitute;
using TeslaTracker.Application.Notifications.EventHandlers;
using TeslaTracker.Application.Notifications.Ports;
using TeslaTracker.Application.Tests.TestSupport;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.Orders.Events;
using Xunit;

namespace TeslaTracker.Application.Tests.Notifications;

public class OrderPushDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static PushChannel ChannelFor(OrderId orderId, string url) =>
        PushChannel.Attach(orderId, PushEndpoint.Create(url, "p256", "auth").Value, "UA", Now);

    private static async IAsyncEnumerable<PushChannel> AsAsync(IEnumerable<PushChannel> source)
    {
        foreach (var c in source) { yield return c; await Task.Yield(); }
    }

    [Fact]
    public async Task VinAssigned_Fans_Out_To_All_Channels_For_Order()
    {
        var orderId = OrderFactory.AnOrderId();
        var ch1 = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/aaa");
        var ch2 = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/bbb");
        var channels = Substitute.For<IPushChannelRepository>();
        channels.FindByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(AsAsync(new[] { ch1, ch2 }));
        var notifier = Substitute.For<IPushNotifier>();
        notifier.SendAsync(Arg.Any<PushChannel>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .Returns(PushDeliveryStatus.Delivered);

        var dispatcher = new OrderPushDispatcher(channels, notifier);
        await dispatcher.HandleAsync(new VinAssigned(orderId, OrderFactory.AVin(), Now), CancellationToken.None);

        await notifier.Received(2).SendAsync(Arg.Any<PushChannel>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Gone_Channel_Is_Removed()
    {
        var orderId = OrderFactory.AnOrderId();
        var ch = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/dead");
        var channels = Substitute.For<IPushChannelRepository>();
        channels.FindByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(AsAsync(new[] { ch }));
        var notifier = Substitute.For<IPushNotifier>();
        notifier.SendAsync(Arg.Any<PushChannel>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .Returns(PushDeliveryStatus.Gone);

        var dispatcher = new OrderPushDispatcher(channels, notifier);
        await dispatcher.HandleAsync(new VinAssigned(orderId, OrderFactory.AVin(), Now), CancellationToken.None);

        await channels.Received(1).RemoveAsync(ch, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_Channel_Below_Threshold_Is_Updated_Not_Removed()
    {
        var orderId = OrderFactory.AnOrderId();
        var ch = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/flaky");
        var channels = Substitute.For<IPushChannelRepository>();
        channels.FindByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(AsAsync(new[] { ch }));
        var notifier = Substitute.For<IPushNotifier>();
        notifier.SendAsync(Arg.Any<PushChannel>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .Returns(PushDeliveryStatus.Failed);

        var dispatcher = new OrderPushDispatcher(channels, notifier);
        await dispatcher.HandleAsync(new VinAssigned(orderId, OrderFactory.AVin(), Now), CancellationToken.None);

        await channels.Received(1).UpdateAsync(ch, Arg.Any<CancellationToken>());
        await channels.DidNotReceive().RemoveAsync(ch, Arg.Any<CancellationToken>());
        ch.FailureCount.Should().Be(1);
    }

    [Fact]
    public async Task State_Change_To_Delivered_Sends_Celebration_Message()
    {
        var orderId = OrderFactory.AnOrderId();
        var ch = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/x");
        var channels = Substitute.For<IPushChannelRepository>();
        channels.FindByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(AsAsync(new[] { ch }));
        var notifier = Substitute.For<IPushNotifier>();
        notifier.SendAsync(Arg.Any<PushChannel>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .Returns(PushDeliveryStatus.Delivered);

        var dispatcher = new OrderPushDispatcher(channels, notifier);
        await dispatcher.HandleAsync(new OrderStateChanged(orderId, OrderState.InTransit, OrderState.Delivered, Now), CancellationToken.None);

        await notifier.Received(1).SendAsync(
            Arg.Is<PushChannel>(c => c == ch),
            Arg.Is<PushPayload>(p => p.Body.Contains("levererad", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Push_Payload_Does_Not_Leak_Sensitive_Strings()
    {
        // Sanity check: payloads should never contain tokens, ciphers, or key ids.
        var orderId = OrderFactory.AnOrderId();
        var ch = ChannelFor(orderId, "https://fcm.googleapis.com/fcm/send/x");
        var channels = Substitute.For<IPushChannelRepository>();
        channels.FindByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(AsAsync(new[] { ch }));

        PushPayload? captured = null;
        var notifier = Substitute.For<IPushNotifier>();
        notifier.SendAsync(Arg.Any<PushChannel>(), Arg.Do<PushPayload>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(PushDeliveryStatus.Delivered);

        var dispatcher = new OrderPushDispatcher(channels, notifier);
        await dispatcher.HandleAsync(new VinAssigned(orderId, OrderFactory.AVin(), Now), CancellationToken.None);

        captured.Should().NotBeNull();
        var serialized = $"{captured!.Title}|{captured.Body}|{captured.Url}";
        serialized.Should().NotContain("refresh", "tokens never go in push payloads");
        serialized.Should().NotContain("cipher");
        serialized.Should().NotContain("kv-key");
    }
}
