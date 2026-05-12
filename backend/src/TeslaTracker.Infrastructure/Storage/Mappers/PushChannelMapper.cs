using TeslaTracker.Domain.Notifications;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Infrastructure.Storage.Entities;

namespace TeslaTracker.Infrastructure.Storage.Mappers;

internal static class PushChannelMapper
{
    public static PushChannelEntity ToEntity(PushChannel channel, string? existingEtag = null)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new PushChannelEntity
        {
            PartitionKey = channel.OrderId.Value,
            RowKey = channel.EndpointHash,
            Endpoint = channel.Endpoint.Url,
            P256dh = channel.Endpoint.P256dh,
            Auth = channel.Endpoint.Auth,
            UserAgent = channel.UserAgent,
            CreatedAt = channel.CreatedAt,
            FailureCount = channel.FailureCount,
            ETag = existingEtag is null ? Azure.ETag.All : new Azure.ETag(existingEtag),
        };
    }

    public static PushChannel ToDomain(PushChannelEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var orderIdResult = OrderId.Create(entity.PartitionKey);
        if (orderIdResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"PushChannelEntity har ogiltigt PartitionKey '{entity.PartitionKey}': {orderIdResult.Error.Message}");
        }

        var endpointResult = PushEndpoint.Create(entity.Endpoint, entity.P256dh, entity.Auth);
        if (endpointResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"PushChannelEntity har trasig endpoint: {endpointResult.Error.Message}");
        }

        return PushChannel.Rehydrate(
            orderIdResult.Value,
            endpointResult.Value,
            entity.UserAgent,
            entity.CreatedAt,
            entity.FailureCount);
    }
}
