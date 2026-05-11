using FluentAssertions;
using TeslaTracker.Domain.Orders;
using Xunit;

namespace TeslaTracker.Domain.Tests.Orders;

public class DeliveryWindowTests
{
    [Fact]
    public void Create_With_Valid_Range_Succeeds()
    {
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);

        var result = DeliveryWindow.Create(start, end, "Juni 2026");

        result.IsSuccess.Should().BeTrue();
        result.Value.Start.Should().Be(start);
        result.Value.End.Should().Be(end);
        result.Value.DisplayText.Should().Be("Juni 2026");
    }

    [Fact]
    public void Create_With_End_Before_Start_Fails()
    {
        var result = DeliveryWindow.Create(
            new DateOnly(2026, 6, 30),
            new DateOnly(2026, 6, 1),
            "ogiltig");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DeliveryWindow.InvalidRange");
    }

    [Fact]
    public void Unknown_Window_Is_Empty()
    {
        var window = DeliveryWindow.Unknown();

        window.Start.Should().BeNull();
        window.End.Should().BeNull();
        window.DisplayText.Should().BeEmpty();
    }

    [Fact]
    public void DeliveryWindow_Has_Value_Equality()
    {
        var a = DeliveryWindow.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), "Juni").Value;
        var b = DeliveryWindow.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), "Juni").Value;

        a.Should().Be(b);
    }
}
