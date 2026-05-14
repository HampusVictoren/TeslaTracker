namespace TeslaTracker.Functions.Turnstile;

internal interface ITurnstileVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
