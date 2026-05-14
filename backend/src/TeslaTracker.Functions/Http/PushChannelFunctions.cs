using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using TeslaTracker.Application.Notifications.Commands.AttachPushChannel;
using TeslaTracker.Application.Notifications.Commands.DetachPushChannel;
using TeslaTracker.Functions.Http.Contracts;

namespace TeslaTracker.Functions.Http;

public sealed class PushChannelFunctions
{
    private readonly AttachPushChannelHandler _attachHandler;
    private readonly DetachPushChannelHandler _detachHandler;

    public PushChannelFunctions(AttachPushChannelHandler attachHandler, DetachPushChannelHandler detachHandler)
    {
        _attachHandler = attachHandler;
        _detachHandler = detachHandler;
    }

    [Function(nameof(AttachPushChannel))]
    public async Task<IResult> AttachPushChannel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/{orderId}/channels")] HttpRequest req,
        string orderId,
        CancellationToken cancellationToken)
    {
        var viewToken = HttpResults.GetBearer(req) ?? string.Empty;
        var body = await req.ReadFromJsonAsync<AttachPushChannelRequest>(cancellationToken);
        if (body is null)
        {
            return Results.BadRequest(new { error = "missing-body" });
        }

        var userAgent = req.Headers.UserAgent.ToString();
        var command = new AttachPushChannelCommand(orderId, viewToken, body.Endpoint, body.P256dh, body.Auth, userAgent);
        var result = await _attachHandler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/orders/{orderId}/channels/{result.Value}", new AttachPushChannelResponse(result.Value))
            : HttpResults.Map(result.Error);
    }

    [Function(nameof(DetachPushChannel))]
    public async Task<IResult> DetachPushChannel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "orders/{orderId}/channels/{endpointHash}")] HttpRequest req,
        string orderId,
        string endpointHash,
        CancellationToken cancellationToken)
    {
        var viewToken = HttpResults.GetBearer(req) ?? string.Empty;
        var command = new DetachPushChannelCommand(orderId, viewToken, endpointHash);
        var result = await _detachHandler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : HttpResults.Map(result.Error);
    }
}
