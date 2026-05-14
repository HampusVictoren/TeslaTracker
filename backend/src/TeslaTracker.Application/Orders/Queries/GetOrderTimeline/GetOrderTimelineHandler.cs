using TeslaTracker.Application.Abstractions;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Orders.Queries.GetOrderTimeline;

public sealed class GetOrderTimelineHandler : IQueryHandler<GetOrderTimelineQuery, IReadOnlyList<OrderHistoryEntry>>
{
    private readonly IOwnerAuthorizer _authorizer;
    private readonly IOrderHistoryReader _reader;

    public GetOrderTimelineHandler(IOwnerAuthorizer authorizer, IOrderHistoryReader reader)
    {
        _authorizer = authorizer;
        _reader = reader;
    }

    public async Task<Result<IReadOnlyList<OrderHistoryEntry>>> HandleAsync(GetOrderTimelineQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var authResult = await _authorizer.AuthorizeAsync(query.OrderId, query.PresentedViewToken, cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<IReadOnlyList<OrderHistoryEntry>>.Failure(authResult.Error);
        }

        var take = query.Take is <= 0 or > 200 ? 50 : query.Take;
        var entries = await _reader.GetTimelineAsync(authResult.Value.Id, take, cancellationToken);
        return Result<IReadOnlyList<OrderHistoryEntry>>.Success(entries);
    }
}
