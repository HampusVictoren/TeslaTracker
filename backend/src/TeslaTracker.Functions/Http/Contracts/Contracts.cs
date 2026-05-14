using TeslaTracker.Domain.Orders;

namespace TeslaTracker.Functions.Http.Contracts;

public sealed record RegisterOrderRequest(string OrderId, string RefreshToken);

public sealed record RegisterOrderResponse(string OrderId, string ViewToken);

public sealed record OrderStatusResponse(
    string OrderId,
    bool IsActive,
    string VehicleModel,
    OrderState State,
    string? Vin,
    DateOnly? DeliveryStart,
    DateOnly? DeliveryEnd,
    string DeliveryDisplay,
    DateTimeOffset LastSyncedAt,
    DateTimeOffset CreatedAt);

public sealed record OrderTimelineResponse(IReadOnlyList<OrderTimelineEntryDto> Entries);

public sealed record OrderTimelineEntryDto(DateTimeOffset OccurredAt, string EventType, string PayloadJson);

public sealed record AttachPushChannelRequest(string Endpoint, string P256dh, string Auth);

public sealed record AttachPushChannelResponse(string EndpointHash);
