using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using TeslaTracker.Application.Orders.Queries.GetOrderTimeline;
using TeslaTracker.Functions.Http.Contracts;

namespace TeslaTracker.Functions.Http;

public sealed class GetOrderTimelineFunction
{
    private readonly GetOrderTimelineHandler _handler;

    public GetOrderTimelineFunction(GetOrderTimelineHandler handler) => _handler = handler;

    [Function(nameof(GetOrderTimeline))]
    public async Task<IResult> GetOrderTimeline(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{orderId}/timeline")] HttpRequest req,
        string orderId,
        CancellationToken cancellationToken)
    {
        var viewToken = HttpResults.GetBearer(req) ?? string.Empty;
        var result = await _handler.HandleAsync(new GetOrderTimelineQuery(orderId, viewToken), cancellationToken);

        if (result.IsFailure)
        {
            return HttpResults.Map(result.Error);
        }

        var response = new OrderTimelineResponse(
            result.Value.Select(e => new OrderTimelineEntryDto(e.OccurredAt, e.EventType, e.PayloadJson)).ToList());
        return Results.Ok(response);
    }
}
