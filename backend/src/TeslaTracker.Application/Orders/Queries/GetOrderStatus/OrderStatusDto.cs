using TeslaTracker.Domain.Orders;

namespace TeslaTracker.Application.Orders.Queries.GetOrderStatus;

public sealed record OrderStatusDto(
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
