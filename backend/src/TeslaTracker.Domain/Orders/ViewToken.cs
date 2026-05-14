using System.Security.Cryptography;
using System.Text;
using TeslaTracker.Domain.SeedWork;

namespace TeslaTracker.Domain.Orders;

public sealed record ViewToken
{
    private const int PlaintextByteLength = 32;
    private const int SaltByteLength = 16;

    public string Hash { get; }
    public string Salt { get; }

    private ViewToken(string hash, string salt)
    {
        Hash = hash;
        Salt = salt;
    }

    public static (ViewToken Token, string Plaintext) Issue()
    {
        var plaintextBytes = RandomNumberGenerator.GetBytes(PlaintextByteLength);
        var saltBytes = RandomNumberGenerator.GetBytes(SaltByteLength);
        var plaintext = Base64UrlEncode(plaintextBytes);
        var salt = Convert.ToHexStringLower(saltBytes);
        var hash = ComputeHash(plaintext, salt);
        return (new ViewToken(hash, salt), plaintext);
    }

    public static Result<ViewToken> Rehydrate(string? hash, string? salt)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return Result<ViewToken>.Failure("ViewToken.MissingHash", "ViewToken-hash saknas.");
        }

        if (string.IsNullOrWhiteSpace(salt))
        {
            return Result<ViewToken>.Failure("ViewToken.MissingSalt", "ViewToken-salt saknas.");
        }

        return new ViewToken(hash, salt);
    }

    public bool Verify(string? plaintextCandidate)
    {
        if (string.IsNullOrWhiteSpace(plaintextCandidate))
        {
            return false;
        }

        var candidateHash = ComputeHash(plaintextCandidate, Salt);
        var expected = Encoding.UTF8.GetBytes(Hash);
        var actual = Encoding.UTF8.GetBytes(candidateHash);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string ComputeHash(string plaintext, string salt)
    {
        var combined = Encoding.UTF8.GetBytes(plaintext + ":" + salt);
        var digest = SHA256.HashData(combined);
        return Convert.ToHexStringLower(digest);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
