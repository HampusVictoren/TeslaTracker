using System.Security.Cryptography;
using System.Text;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Notifications;

public sealed class PushChannel : AggregateRoot
{
    public const int MaxFailures = 3;

    public OrderId OrderId { get; }
    public string EndpointHash { get; }
    public PushEndpoint Endpoint { get; private set; }
    public string UserAgent { get; }
    public DateTimeOffset CreatedAt { get; }
    public int FailureCount { get; private set; }

    private PushChannel(
        OrderId orderId,
        string endpointHash,
        PushEndpoint endpoint,
        string userAgent,
        DateTimeOffset createdAt,
        int failureCount)
    {
        OrderId = orderId;
        EndpointHash = endpointHash;
        Endpoint = endpoint;
        UserAgent = userAgent;
        CreatedAt = createdAt;
        FailureCount = failureCount;
    }

    public static PushChannel Attach(OrderId orderId, PushEndpoint endpoint, string userAgent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(endpoint);

        var hash = ComputeHash(endpoint.Url);
        return new PushChannel(orderId, hash, endpoint, userAgent ?? string.Empty, now, 0);
    }

    public static PushChannel Rehydrate(
        OrderId orderId,
        PushEndpoint endpoint,
        string userAgent,
        DateTimeOffset createdAt,
        int failureCount)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentNullException.ThrowIfNull(endpoint);

        var hash = ComputeHash(endpoint.Url);
        return new PushChannel(orderId, hash, endpoint, userAgent ?? string.Empty, createdAt, failureCount);
    }

    public void RecordSuccess() => FailureCount = 0;

    public bool RecordFailure()
    {
        FailureCount++;
        return FailureCount >= MaxFailures;
    }

    private static string ComputeHash(string endpointUrl)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(endpointUrl));
        return Convert.ToHexStringLower(bytes);
    }
}
