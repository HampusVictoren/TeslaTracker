using FluentAssertions;
using TeslaTracker.Infrastructure.Crypto;
using Xunit;

namespace TeslaTracker.Infrastructure.Tests.Crypto;

public class EnvelopeCipherTests
{
    [Fact]
    public void Pack_Then_Unpack_Roundtrips_All_Three_Parts()
    {
        var nonce = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var tag = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99 };
        var ciphertext = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };

        var blob = EnvelopeCipher.Pack(nonce, tag, ciphertext);
        var (n, t, c) = EnvelopeCipher.Unpack(blob);

        n.ToArray().Should().Equal(nonce);
        t.ToArray().Should().Equal(tag);
        c.ToArray().Should().Equal(ciphertext);
    }

    [Fact]
    public void Pack_Layout_Has_Expected_Total_Length()
    {
        var nonce = new byte[12];
        var tag = new byte[16];
        var ciphertext = new byte[50];

        var blob = EnvelopeCipher.Pack(nonce, tag, ciphertext);

        blob.Length.Should().Be(4 + 12 + 4 + 16 + 50);
    }

    [Fact]
    public void Unpack_Throws_On_Truncated_Blob()
    {
        var truncated = new byte[] { 1, 2, 3 };

        var act = () => EnvelopeCipher.Unpack(truncated);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Unpack_Throws_On_Invalid_Nonce_Length_Header()
    {
        var bogus = new byte[] { 0xFF, 0xFF, 0xFF, 0x7F, 1, 2, 3, 4 };

        var act = () => EnvelopeCipher.Unpack(bogus);

        act.Should().Throw<ArgumentException>();
    }
}
