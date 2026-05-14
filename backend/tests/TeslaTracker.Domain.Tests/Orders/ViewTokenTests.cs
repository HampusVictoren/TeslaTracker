using FluentAssertions;
using TeslaTracker.Domain.Orders;
using Xunit;

namespace TeslaTracker.Domain.Tests.Orders;

public class ViewTokenTests
{
    [Fact]
    public void Issue_Returns_Different_Plaintext_Each_Call()
    {
        var (_, plaintextA) = ViewToken.Issue();
        var (_, plaintextB) = ViewToken.Issue();

        plaintextA.Should().NotBe(plaintextB);
    }

    [Fact]
    public void Issue_Returns_Different_Salt_Each_Call()
    {
        var (tokenA, _) = ViewToken.Issue();
        var (tokenB, _) = ViewToken.Issue();

        tokenA.Salt.Should().NotBe(tokenB.Salt);
    }

    [Fact]
    public void Verify_With_Correct_Plaintext_Returns_True()
    {
        var (token, plaintext) = ViewToken.Issue();

        token.Verify(plaintext).Should().BeTrue();
    }

    [Fact]
    public void Verify_With_Wrong_Plaintext_Returns_False()
    {
        var (token, _) = ViewToken.Issue();

        token.Verify("wrong-plaintext-here").Should().BeFalse();
    }

    [Fact]
    public void Verify_With_Empty_Or_Null_Returns_False()
    {
        var (token, _) = ViewToken.Issue();

        token.Verify(null).Should().BeFalse();
        token.Verify("").Should().BeFalse();
        token.Verify("   ").Should().BeFalse();
    }

    [Fact]
    public void Verify_Against_Rehydrated_Token_Still_Works()
    {
        var (original, plaintext) = ViewToken.Issue();
        var rehydrated = ViewToken.Rehydrate(original.Hash, original.Salt).Value;

        rehydrated.Verify(plaintext).Should().BeTrue();
        rehydrated.Verify("wrong").Should().BeFalse();
    }

    [Fact]
    public void Rehydrate_With_Missing_Hash_Fails()
    {
        var result = ViewToken.Rehydrate(null, "abcd");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ViewToken.MissingHash");
    }

    [Fact]
    public void Rehydrate_With_Missing_Salt_Fails()
    {
        var result = ViewToken.Rehydrate("hash", "");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ViewToken.MissingSalt");
    }

    [Fact]
    public void Plaintext_Is_Url_Safe()
    {
        var (_, plaintext) = ViewToken.Issue();

        plaintext.Should().NotContain("+");
        plaintext.Should().NotContain("/");
        plaintext.Should().NotContain("=");
    }

    [Fact]
    public void Different_Tokens_With_Same_Plaintext_Yield_Different_Hashes()
    {
        // Sanity: same plaintext but different salt → different hash
        var (a, _) = ViewToken.Issue();
        var (b, _) = ViewToken.Issue();

        a.Hash.Should().NotBe(b.Hash);
    }
}
