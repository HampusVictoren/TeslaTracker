namespace TeslaTracker.Domain.Orders;

public sealed record OrderSnapshot(
    Vin? Vin,
    DeliveryWindow DeliveryWindow,
    string VehicleModel,
    OrderState State,
    string RawHash);
