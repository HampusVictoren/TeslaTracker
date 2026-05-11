using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Application.Abstractions;

public interface ICommandHandler<in TCommand, TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand>
{
    Task<Result<Unit>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public readonly record struct Unit
{
    public static Unit Value => default;
}
