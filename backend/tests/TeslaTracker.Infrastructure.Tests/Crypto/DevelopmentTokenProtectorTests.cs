using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeslaTracker.Infrastructure.Crypto;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.Crypto;

public class DevelopmentTokenProtectorTests
{
    private static DevelopmentTokenProtector CreateProtector(out byte[] key)
    {
        key = RandomNumberGenerator.GetBytes(32);
        var options = Options.Create(new CryptoOptions { DevKey = Convert.ToBase64String(key) });
        return new DevelopmentTokenProtector(options, NullLogger<DevelopmentTokenProtector>.Instance);
    }

    [Fact]
    public async Task Protect_Then_Unprotect_Roundtrips_Token()
    {
        var protector = CreateProtector(out _);
        var token = "qts-1234567890-abcdefghij";

        var secret = await protector.ProtectAsync(token, CancellationToken.None);
        var unprotected = await protector.UnprotectAsync(secret, CancellationToken.None);

        unprotected.IsSuccess.Should().BeTrue();
        unprotected.Value.Should().Be(token);
    }

    [Fact]
    public async Task Protect_Produces_Different_Cipher_For_Same_Input()
    {
        var protector = CreateProtector(out _);

        var first = await protector.ProtectAsync("same-token", CancellationToken.None);
        var second = await protector.ProtectAsync("same-token", CancellationToken.None);

        first.Cipher.ToArray().Should().NotEqual(second.Cipher.ToArray(),
            "AES-GCM with fresh nonce should produce different ciphertext per call");
    }

    [Fact]
    public async Task Unprotect_With_Wrong_KeyId_Returns_Failure()
    {
        var protector = CreateProtector(out _);
        var secret = await protector.ProtectAsync("token", CancellationToken.None);

        var tampered = TeslaTracker.Domain.Orders.TrackingSecret.Create(secret.Cipher, "different-key").Value;
        var result = await protector.UnprotectAsync(tampered, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TokenProtector.KeyIdMismatch");
    }

    [Fact]
    public async Task Unprotect_With_Wrong_Key_Returns_DecryptionFailed()
    {
        var protectorA = CreateProtector(out _);
        var secret = await protectorA.ProtectAsync("token", CancellationToken.None);

        var protectorB = CreateProtector(out _);
        var result = await protectorB.UnprotectAsync(secret, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TokenProtector.DecryptionFailed");
    }

    [Fact]
    public void Constructor_Throws_When_DevKey_Missing()
    {
        var options = Options.Create(new CryptoOptions { DevKey = null });

        var act = () => new DevelopmentTokenProtector(options, NullLogger<DevelopmentTokenProtector>.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Crypto:DevKey saknas*");
    }

    [Fact]
    public void Constructor_Throws_When_DevKey_Wrong_Length()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var options = Options.Create(new CryptoOptions { DevKey = shortKey });

        var act = () => new DevelopmentTokenProtector(options, NullLogger<DevelopmentTokenProtector>.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }
}
