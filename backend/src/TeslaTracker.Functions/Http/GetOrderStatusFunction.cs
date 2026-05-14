using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using TeslaTracker.Application.Orders.Queries.GetOrderStatus;
using TeslaTracker.Functions.Http.Contracts;

namespace TeslaTracker.Functions.Http;

public sealed class GetOrderStatusFunction
{
    private readonly GetOrderStatusHandler _handler;

    public GetOrderStatusFunction(GetOrderStatusHandler handler) => _handler = handler;

    [Function(nameof(GetOrderStatus))]
    public async Task<IResult> GetOrderStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{orderId}")] HttpRequest req,
        string orderId,
        CancellationToken cancellationToken)
    {
        var viewToken = HttpResults.GetBearer(req) ?? string.Empty;
        var result = await _handler.HandleAsync(new GetOrderStatusQuery(orderId, viewToken), cancellationToken);

        if (result.IsFailure)
        {
            return HttpResults.Map(result.Error);
        }

        var dto = result.Value;
        var response = new OrderStatusResponse(
            dto.OrderId, dto.IsActive, dto.VehicleModel, dto.State,
            dto.Vin, dto.DeliveryStart, dto.DeliveryEnd, dto.DeliveryDisplay,
            dto.LastSyncedAt, dto.CreatedAt);
        return Results.Ok(response);
    }
}
