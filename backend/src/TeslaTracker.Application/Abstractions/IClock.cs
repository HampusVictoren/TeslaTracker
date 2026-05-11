namespace TeslaTracker.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
