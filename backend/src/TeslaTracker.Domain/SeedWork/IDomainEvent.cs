namespace TeslaTracker.Domain.SeedWork;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
