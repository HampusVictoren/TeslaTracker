using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Notifications.Commands.AttachPushChannel;

public sealed class AttachPushChannelHandler : ICommandHandler<AttachPushChannelCommand, string>
{
    private readonly IOwnerAuthorizer _authorizer;
    private readonly IPushChannelRepository _channels;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AttachPushChannelHandler(
        IOwnerAuthorizer authorizer,
        IPushChannelRepository channels,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _authorizer = authorizer;
        _channels = channels;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<string>> HandleAsync(AttachPushChannelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authResult = await _authorizer.AuthorizeAsync(command.OrderId, command.PresentedViewToken, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<string>.Failure(authResult.Error);
        }

        var endpointResult = PushEndpoint.Create(command.Endpoint, command.P256dh, command.Auth);
        if (endpointResult.IsFailure)
        {
            return Result<string>.Failure(endpointResult.Error);
        }

        var channel = PushChannel.Attach(authResult.Value.Id, endpointResult.Value, command.UserAgent, _clock.UtcNow);
        await _channels.AddAsync(channel, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(channel.EndpointHash);
    }
}
