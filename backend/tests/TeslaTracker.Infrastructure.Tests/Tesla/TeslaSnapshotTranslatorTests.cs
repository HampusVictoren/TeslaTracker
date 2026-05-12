using FluentAssertions;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Tesla;
using TeslaTracker.Infrastructure.Tesla.Dto;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.Tesla;

public class TeslaSnapshotTranslatorTests
{
    private static TeslaOrderDto Dto(
        string referenceNumber = "RN123456789",
        string? vin = null,
        string? modelCode = "MY",
        string? orderStatus = "ORDER_PLACED",
        string? deliveryWindowDisplay = null,
        string? deliveryWindowStart = null,
        string? deliveryWindowEnd = null) =>
        new(referenceNumber, vin, modelCode, orderStatus, deliveryWindowDisplay, deliveryWindowStart, deliveryWindowEnd);

    [Fact]
    public void Null_Vin_Produces_OrderSnapshot_Without_Vin()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(vin: null));

        snapshot.Vin.Should().BeNull();
    }

    [Fact]
    public void Empty_Vin_Produces_OrderSnapshot_Without_Vin()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(vin: "   "));

        snapshot.Vin.Should().BeNull();
    }

    [Fact]
    public void Invalid_Vin_Produces_OrderSnapshot_Without_Vin_Silently()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(vin: "TOOSHORT"));

        snapshot.Vin.Should().BeNull();
    }

    [Fact]
    public void Lowercase_Vin_Is_Normalized()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(vin: "5yjygdee0lf000001"));

        snapshot.Vin.Should().NotBeNull();
        snapshot.Vin!.Value.Should().Be("5YJYGDEE0LF000001");
    }

    [Theory]
    [InlineData("RESERVED", OrderState.Reserved)]
    [InlineData("ORDER_PLACED", OrderState.OrderPlaced)]
    [InlineData("ORDERPLACED", OrderState.OrderPlaced)]
    [InlineData("IN_PRODUCTION", OrderState.InProduction)]
    [InlineData("BUILT", OrderState.Built)]
    [InlineData("IN_TRANSIT", OrderState.InTransit)]
    [InlineData("READY_FOR_DELIVERY", OrderState.ReadyForDelivery)]
    [InlineData("DELIVERED", OrderState.Delivered)]
    [InlineData("CANCELED", OrderState.Canceled)]
    [InlineData("CANCELLED", OrderState.Canceled)]
    [InlineData("UNKNOWN_FUTURE_STATE", OrderState.Unknown)]
    [InlineData(null, OrderState.Unknown)]
    [InlineData("", OrderState.Unknown)]
    public void OrderStatus_Maps_To_Expected_OrderState(string? status, OrderState expected)
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(orderStatus: status));

        snapshot.State.Should().Be(expected);
    }

    [Fact]
    public void DeliveryWindow_With_Only_Display_Has_Null_Dates()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(deliveryWindowDisplay: "Q4 2026"));

        snapshot.DeliveryWindow.Start.Should().BeNull();
        snapshot.DeliveryWindow.End.Should().BeNull();
        snapshot.DeliveryWindow.DisplayText.Should().Be("Q4 2026");
    }

    [Fact]
    public void DeliveryWindow_With_Iso_Dates_Parses_Successfully()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(
            deliveryWindowStart: "2026-06-01",
            deliveryWindowEnd: "2026-06-30",
            deliveryWindowDisplay: "Juni 2026"));

        snapshot.DeliveryWindow.Start.Should().Be(new DateOnly(2026, 6, 1));
        snapshot.DeliveryWindow.End.Should().Be(new DateOnly(2026, 6, 30));
        snapshot.DeliveryWindow.DisplayText.Should().Be("Juni 2026");
    }

    [Fact]
    public void DeliveryWindow_With_Malformed_Date_Falls_Back_To_Unknown()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(
            deliveryWindowStart: "not-a-date",
            deliveryWindowEnd: "also-bad"));

        snapshot.DeliveryWindow.Start.Should().BeNull();
        snapshot.DeliveryWindow.End.Should().BeNull();
    }

    [Fact]
    public void Missing_ModelCode_Defaults_To_Unknown()
    {
        var snapshot = new TeslaSnapshotTranslator().Translate(Dto(modelCode: null));

        snapshot.VehicleModel.Should().Be("Unknown");
    }

    [Fact]
    public void Same_Dto_Produces_Identical_Hash()
    {
        var dto = Dto(vin: "5YJYGDEE0LF000001", orderStatus: "ORDER_PLACED");

        var a = new TeslaSnapshotTranslator().Translate(dto);
        var b = new TeslaSnapshotTranslator().Translate(dto);

        a.RawHash.Should().Be(b.RawHash);
    }

    [Fact]
    public void Different_Vin_Produces_Different_Hash()
    {
        var a = new TeslaSnapshotTranslator().Translate(Dto(vin: null));
        var b = new TeslaSnapshotTranslator().Translate(Dto(vin: "5YJYGDEE0LF000001"));

        a.RawHash.Should().NotBe(b.RawHash);
    }

    [Fact]
    public void Status_Casing_Does_Not_Change_Hash()
    {
        var a = new TeslaSnapshotTranslator().Translate(Dto(orderStatus: "ORDER_PLACED"));
        var b = new TeslaSnapshotTranslator().Translate(Dto(orderStatus: "order_placed"));

        a.RawHash.Should().Be(b.RawHash);
    }
}
