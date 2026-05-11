using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Abstractions;

public interface IQueryHandler<in TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
