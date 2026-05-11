using TeslaTracker.Application.Abstractions;
using TeslaTracker.Application.Tesla;
using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Orders.Commands.RegisterOrderTracking;

public sealed class RegisterOrderTrackingHandler : ICommandHandler<RegisterOrderTrackingCommand, OrderId>
{
    private const int MaxRegistrationsPerMinutePerIp = 3;

    private readonly IOrderRepository _orders;
    private readonly ITeslaOrderGateway _tesla;
    private readonly ITokenProtector _tokenProtector;
    private readonly IRateLimiter _rateLimiter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RegisterOrderTrackingHandler(
        IOrderRepository orders,
        ITeslaOrderGateway tesla,
        ITokenProtector tokenProtector,
        IRateLimiter rateLimiter,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _orders = orders;
        _tesla = tesla;
        _tokenProtector = tokenProtector;
        _rateLimiter = rateLimiter;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OrderId>> HandleAsync(RegisterOrderTrackingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var orderIdResult = OrderId.Create(command.OrderId);
        if (orderIdResult.IsFailure)
        {
            return Result<OrderId>.Failure(orderIdResult.Error);
        }

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result<OrderId>.Failure("Registration.MissingToken", "Refresh token saknas.");
        }

        if (!await _rateLimiter.TryAcquireAsync(
                $"ip:{command.ClientIpAddress}", MaxRegistrationsPerMinutePerIp, cancellationToken))
        {
            return Result<OrderId>.Failure("Registration.RateLimited", "För många registreringar — försök igen om en minut.");
        }

        var orderId = orderIdResult.Value;

        var existing = await _orders.FindAsync(orderId, cancellationToken);
        if (existing is { IsActive: true })
        {
            return Result<OrderId>.Failure("Registration.AlreadyTracked", "Ordern spåras redan.");
        }

        var teslaResult = await _tesla.FetchOrderAsync(orderId, new TeslaCredential(command.RefreshToken), cancellationToken);
        if (teslaResult.IsFailure)
        {
            return Result<OrderId>.Failure(teslaResult.Error);
        }

        var protectedSecret = await _tokenProtector.ProtectAsync(teslaResult.Value.NewRefreshToken, cancellationToken);

        var now = _clock.UtcNow;

        if (existing is not null)
        {
            existing.Reactivate(protectedSecret, teslaResult.Value.Snapshot, now);
            await _orders.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            var order = Order.Register(orderId, protectedSecret, teslaResult.Value.Snapshot, now);
            await _orders.AddAsync(order, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<OrderId>.Success(orderId);
    }
}
