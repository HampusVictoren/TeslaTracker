using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeslaTracker.Application.Tokens;
using TeslaTracker.Domain.Orders;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Infrastructure.Crypto;

public sealed class DevelopmentTokenProtector : ITokenProtector
{
    public const string DevKeyId = "dev-key-v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;
    private readonly ILogger<DevelopmentTokenProtector> _logger;

    public DevelopmentTokenProtector(IOptions<CryptoOptions> options, ILogger<DevelopmentTokenProtector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;

        var rawKey = options.Value.DevKey
            ?? throw new InvalidOperationException("Crypto:DevKey saknas — sätt User Secret 'Crypto:DevKey' till 32 random bytes (base64).");

        _key = Convert.FromBase64String(rawKey);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException($"Crypto:DevKey måste vara 32 bytes; fick {_key.Length}.");
        }
    }

    public Task<TrackingSecret> ProtectAsync(string plaintextRefreshToken, CancellationToken cancellationToken)
    {
        _logger.LogWarning("DEVELOPMENT TOKEN PROTECTOR ACTIVE — NEVER USE IN PRODUCTION.");

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextRefreshToken);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var blob = EnvelopeCipher.Pack(nonce, tag, ciphertext);
        var result = TrackingSecret.Create(blob, DevKeyId);
        return Task.FromResult(result.Value);
    }

    public Task<Result<string>> UnprotectAsync(TrackingSecret secret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);

        if (secret.KeyId != DevKeyId)
        {
            return Task.FromResult(Result<string>.Failure(
                "TokenProtector.KeyIdMismatch",
                $"Förväntade key-id '{DevKeyId}', fick '{secret.KeyId}'."));
        }

        try
        {
            var (nonce, tag, ciphertext) = EnvelopeCipher.Unpack(secret.Cipher);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce.Span, ciphertext.Span, tag.Span, plaintext);

            return Task.FromResult(Result<string>.Success(Encoding.UTF8.GetString(plaintext)));
        }
        catch (CryptographicException ex)
        {
            return Task.FromResult(Result<string>.Failure(
                "TokenProtector.DecryptionFailed",
                $"Kunde inte dekryptera token: {ex.Message}"));
        }
    }
}
