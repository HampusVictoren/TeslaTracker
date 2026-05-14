using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;
using TeslaTracker.Functions.Attributes;
using TeslaTracker.Functions.Http.Contracts;

namespace TeslaTracker.Functions.Http;

public sealed class RegisterOrderFunction
{
    private readonly RegisterOrderTrackingHandler _handler;

    public RegisterOrderFunction(RegisterOrderTrackingHandler handler) => _handler = handler;

    [Function(nameof(RegisterOrder))]
    [RequireTurnstile]
    public async Task<IResult> RegisterOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadFromJsonAsync<RegisterOrderRequest>(cancellationToken);
        if (body is null)
        {
            return Results.BadRequest(new { error = "missing-body" });
        }

        var command = new RegisterOrderTrackingCommand(body.OrderId, body.RefreshToken, HttpResults.GetClientIp(req));
        var result = await _handler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return HttpResults.Map(result.Error);
        }

        var response = new RegisterOrderResponse(result.Value.OrderId.Value, result.Value.ViewTokenPlaintext);
        return Results.Created($"/api/orders/{response.OrderId}", response);
    }
}
