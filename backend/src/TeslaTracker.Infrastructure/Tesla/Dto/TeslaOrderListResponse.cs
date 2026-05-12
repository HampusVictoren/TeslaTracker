using System.Text.Json.Serialization;

namespace TeslaTracker.Infrastructure.Tesla.Dto;

internal sealed record TeslaOrderListResponse(
    [property: JsonPropertyName("response")] IReadOnlyList<TeslaOrderDto> Response);
