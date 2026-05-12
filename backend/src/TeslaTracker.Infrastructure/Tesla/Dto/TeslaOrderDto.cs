using System.Text.Json.Serialization;

namespace TeslaTracker.Infrastructure.Tesla.Dto;

internal sealed record TeslaOrderDto(
    [property: JsonPropertyName("referenceNumber")] string ReferenceNumber,
    [property: JsonPropertyName("vin")] string? Vin,
    [property: JsonPropertyName("modelCode")] string? ModelCode,
    [property: JsonPropertyName("orderStatus")] string? OrderStatus,
    [property: JsonPropertyName("deliveryWindowDisplay")] string? DeliveryWindowDisplay,
    [property: JsonPropertyName("deliveryWindowStart")] string? DeliveryWindowStart,
    [property: JsonPropertyName("deliveryWindowEnd")] string? DeliveryWindowEnd);
