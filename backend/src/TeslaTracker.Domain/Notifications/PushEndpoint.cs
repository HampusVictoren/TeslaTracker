using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Notifications;

public sealed record PushEndpoint
{
    public string Url { get; }
    public string P256dh { get; }
    public string Auth { get; }

    private PushEndpoint(string url, string p256dh, string auth)
    {
        Url = url;
        P256dh = p256dh;
        Auth = auth;
    }

    public static Result<PushEndpoint> Create(string? url, string? p256dh, string? auth)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result<PushEndpoint>.Failure("PushEndpoint.MissingUrl", "Endpoint-URL saknas.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
        {
            return Result<PushEndpoint>.Failure("PushEndpoint.InvalidUrl", "Endpoint måste vara en absolut HTTPS-URL.");
        }

        if (string.IsNullOrWhiteSpace(p256dh))
        {
            return Result<PushEndpoint>.Failure("PushEndpoint.MissingP256dh", "P256DH-nyckel saknas.");
        }

        if (string.IsNullOrWhiteSpace(auth))
        {
            return Result<PushEndpoint>.Failure("PushEndpoint.MissingAuth", "Auth-secret saknas.");
        }

        return new PushEndpoint(parsed.ToString(), p256dh.Trim(), auth.Trim());
    }
}
