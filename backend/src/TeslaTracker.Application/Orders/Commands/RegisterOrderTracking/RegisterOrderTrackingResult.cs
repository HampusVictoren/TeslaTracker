using TeslaTracker.Domain.Orders;

namespace TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;

public sealed record RegisterOrderTrackingResult(OrderId OrderId, string ViewTokenPlaintext);
