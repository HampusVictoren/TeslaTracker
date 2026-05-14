namespace TeslaTracker.Application.Orders.Queries.GetOrderTimeline;

public sealed record GetOrderTimelineQuery(string OrderId, string PresentedViewToken, int Take = 50);
