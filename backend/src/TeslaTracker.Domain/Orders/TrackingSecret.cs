using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders;

public sealed record TrackingSecret
{
    public ReadOnlyMemory<byte> Cipher { get; }
    public string KeyId { get; }

    private TrackingSecret(ReadOnlyMemory<byte> cipher, string keyId)
    {
        Cipher = cipher;
        KeyId = keyId;
    }

    public static Result<TrackingSecret> Create(ReadOnlyMemory<byte> cipher, string? keyId)
    {
        if (cipher.IsEmpty)
        {
            return Result<TrackingSecret>.Failure("TrackingSecret.Empty", "Krypterad token saknas.");
        }

        if (string.IsNullOrWhiteSpace(keyId))
        {
            return Result<TrackingSecret>.Failure("TrackingSecret.MissingKeyId", "Key Vault key-id saknas.");
        }

        return new TrackingSecret(cipher, keyId.Trim());
    }
}
