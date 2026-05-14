using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Orders.Commands.StopTracking;

namespace TeslaTracker.Functions.Http;

public sealed class StopTrackingFunction
{
    private readonly StopTrackingHandler _handler;
    private readonly IOwnerAuthorizer _authorizer;

    public StopTrackingFunction(StopTrackingHandler handler, IOwnerAuthorizer authorizer)
    {
        _handler = handler;
        _authorizer = authorizer;
    }

    [Function(nameof(StopTracking))]
    public async Task<IResult> StopTracking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "orders/{orderId}")] HttpRequest req,
        string orderId,
        CancellationToken cancellationToken)
    {
        var viewToken = HttpResults.GetBearer(req) ?? string.Empty;
        var auth = await _authorizer.AuthorizeAsync(orderId, viewToken, cancellationToken);
        if (auth.IsFailure)
        {
            return HttpResults.Map(auth.Error);
        }

        var result = await _handler.HandleAsync(new StopTrackingCommand(orderId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : HttpResults.Map(result.Error);
    }
}
