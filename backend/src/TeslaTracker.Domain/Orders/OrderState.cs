namespace TeslaTracker.Domain.Orders;

public enum OrderState
{
    Unknown,
    Reserved,
    OrderPlaced,
    InProduction,
    Built,
    InTransit,
    ReadyForDelivery,
    Delivered,
    Canceled,
}
