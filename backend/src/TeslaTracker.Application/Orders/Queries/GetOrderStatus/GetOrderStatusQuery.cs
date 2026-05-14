namespace TeslaTracker.Application.Orders.Queries.GetOrderStatus;

public sealed record GetOrderStatusQuery(string OrderId, string PresentedViewToken);
