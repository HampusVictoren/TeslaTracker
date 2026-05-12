using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaTracker.Domain.Orders;

namespace TeslaTracker.Infrastructure.Storage.Mappers;

internal static class SnapshotJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    public static string Serialize(OrderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var dto = new SnapshotDto(
            snapshot.Vin?.Value,
            snapshot.DeliveryWindow.Start,
            snapshot.DeliveryWindow.End,
            snapshot.DeliveryWindow.DisplayText,
            snapshot.VehicleModel,
            snapshot.State,
            snapshot.RawHash);

        return JsonSerializer.Serialize(dto, Options);
    }

    public static OrderSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OrderSnapshot(null, DeliveryWindow.Unknown(), "Unknown", OrderState.Unknown, string.Empty);
        }

        var dto = JsonSerializer.Deserialize<SnapshotDto>(json, Options)
            ?? throw new InvalidOperationException("Kunde inte deserialisera OrderSnapshot.");

        var vin = string.IsNullOrWhiteSpace(dto.Vin) ? null : Vin.Create(dto.Vin).Value;
        var window = DeliveryWindow.Create(dto.DeliveryWindowStart, dto.DeliveryWindowEnd, dto.DeliveryWindowDisplay).Value;
        return new OrderSnapshot(vin, window, dto.VehicleModel, dto.State, dto.RawHash);
    }

    private sealed record SnapshotDto(
        string? Vin,
        DateOnly? DeliveryWindowStart,
        DateOnly? DeliveryWindowEnd,
        string DeliveryWindowDisplay,
        string VehicleModel,
        OrderState State,
        string RawHash);
}
