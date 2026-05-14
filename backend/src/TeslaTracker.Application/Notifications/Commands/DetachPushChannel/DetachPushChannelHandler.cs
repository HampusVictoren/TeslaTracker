using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Notifications.Commands.DetachPushChannel;

public sealed class DetachPushChannelHandler : ICommandHandler<DetachPushChannelCommand>
{
    private readonly IOwnerAuthorizer _authorizer;
    private readonly IPushChannelRepository _channels;
    private readonly IUnitOfWork _unitOfWork;

    public DetachPushChannelHandler(
        IOwnerAuthorizer authorizer,
        IPushChannelRepository channels,
        IUnitOfWork unitOfWork)
    {
        _authorizer = authorizer;
        _channels = channels;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> HandleAsync(DetachPushChannelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authResult = await _authorizer.AuthorizeAsync(command.OrderId, command.PresentedViewToken, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<Unit>.Failure(authResult.Error);
        }

        var channel = await _channels.FindAsync(authResult.Value.Id, command.EndpointHash, cancellationToken);
        if (channel is null)
        {
            return Result<Unit>.Failure("PushChannel.NotFound", "Push-kanalen finns inte.");
        }

        await _channels.RemoveAsync(channel, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
