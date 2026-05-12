using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Tesla.Dto;

namespace TeslaTracker.Infrastructure.Tesla;

internal sealed class TeslaSnapshotTranslator
{
    public OrderSnapshot Translate(TeslaOrderDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var vin = TryParseVin(dto.Vin);
        var window = ParseDeliveryWindow(dto);
        var state = MapOrderState(dto.OrderStatus);
        var model = string.IsNullOrWhiteSpace(dto.ModelCode) ? "Unknown" : dto.ModelCode.Trim();
        var hash = ComputeHash(dto);

        return new OrderSnapshot(vin, window, model, state, hash);
    }

    private static Vin? TryParseVin(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var result = Vin.Create(raw);
        return result.IsSuccess ? result.Value : null;
    }

    private static DeliveryWindow ParseDeliveryWindow(TeslaOrderDto dto)
    {
        var start = TryParseDate(dto.DeliveryWindowStart);
        var end = TryParseDate(dto.DeliveryWindowEnd);
        var display = dto.DeliveryWindowDisplay ?? string.Empty;

        var result = DeliveryWindow.Create(start, end, display);
        return result.IsSuccess ? result.Value : DeliveryWindow.Unknown();
    }

    private static DateOnly? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            return DateOnly.FromDateTime(dt.UtcDateTime);
        }

        return null;
    }

    private static OrderState MapOrderState(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        null or "" => OrderState.Unknown,
        "RESERVED" => OrderState.Reserved,
        "ORDER_PLACED" or "ORDERPLACED" => OrderState.OrderPlaced,
        "IN_PRODUCTION" or "INPRODUCTION" or "PRODUCING" => OrderState.InProduction,
        "BUILT" or "BUILDING_COMPLETED" => OrderState.Built,
        "IN_TRANSIT" or "INTRANSIT" or "SHIPPED" => OrderState.InTransit,
        "READY_FOR_DELIVERY" or "READYFORDELIVERY" => OrderState.ReadyForDelivery,
        "DELIVERED" => OrderState.Delivered,
        "CANCELLED" or "CANCELED" => OrderState.Canceled,
        _ => OrderState.Unknown,
    };

    private static string ComputeHash(TeslaOrderDto dto)
    {
        var canonical = new
        {
            dto.ReferenceNumber,
            Vin = dto.Vin?.Trim().ToUpperInvariant() ?? string.Empty,
            ModelCode = dto.ModelCode?.Trim() ?? string.Empty,
            OrderStatus = dto.OrderStatus?.Trim().ToUpperInvariant() ?? string.Empty,
            DeliveryWindowDisplay = dto.DeliveryWindowDisplay?.Trim() ?? string.Empty,
            DeliveryWindowStart = dto.DeliveryWindowStart ?? string.Empty,
            DeliveryWindowEnd = dto.DeliveryWindowEnd ?? string.Empty,
        };

        var json = JsonSerializer.Serialize(canonical);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }
}
