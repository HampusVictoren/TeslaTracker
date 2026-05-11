using FluentAssertions;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using Xunit;

namespace TeslaTracker.Domain.Tests.Notifications;

public class PushChannelTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static OrderId AnOrderId() => OrderId.Create("RN123456789").Value;

    private static PushEndpoint AnEndpoint(string url = "https://fcm.googleapis.com/fcm/send/abc") =>
        PushEndpoint.Create(url, "key-p256dh", "auth-secret").Value;

    [Fact]
    public void Attach_Creates_Channel_With_Deterministic_Hash()
    {
        var a = PushChannel.Attach(AnOrderId(), AnEndpoint(), "Mozilla", Now);
        var b = PushChannel.Attach(AnOrderId(), AnEndpoint(), "Other UA", Now.AddHours(1));

        a.EndpointHash.Should().Be(b.EndpointHash);
        a.EndpointHash.Should().HaveLength(64);
    }

    [Fact]
    public void Different_Endpoints_Produce_Different_Hashes()
    {
        var a = PushChannel.Attach(AnOrderId(), AnEndpoint("https://fcm.googleapis.com/fcm/send/aaa"), "UA", Now);
        var b = PushChannel.Attach(AnOrderId(), AnEndpoint("https://fcm.googleapis.com/fcm/send/bbb"), "UA", Now);

        a.EndpointHash.Should().NotBe(b.EndpointHash);
    }

    [Fact]
    public void RecordFailure_Returns_True_At_Threshold()
    {
        var channel = PushChannel.Attach(AnOrderId(), AnEndpoint(), "UA", Now);

        channel.RecordFailure().Should().BeFalse();
        channel.RecordFailure().Should().BeFalse();
        channel.RecordFailure().Should().BeTrue();
    }

    [Fact]
    public void RecordSuccess_Resets_Failure_Count()
    {
        var channel = PushChannel.Attach(AnOrderId(), AnEndpoint(), "UA", Now);
        channel.RecordFailure();
        channel.RecordFailure();

        channel.RecordSuccess();

        channel.FailureCount.Should().Be(0);
    }

    [Theory]
    [InlineData("http://insecure.example.com", "PushEndpoint.InvalidUrl")]
    [InlineData("not-a-url", "PushEndpoint.InvalidUrl")]
    public void Endpoint_Creation_Fails_For_Invalid_Url(string url, string expectedCode)
    {
        var result = PushEndpoint.Create(url, "p256", "auth");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }
}
