namespace TeslaTracker.Application.Abstractions;

public interface IRateLimiter
{
    Task<bool> TryAcquireAsync(string key, int maxPerMinute, CancellationToken cancellationToken);
}
