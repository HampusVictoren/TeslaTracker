using FluentAssertions;
using TeslaTracker.Domain.Orders;
using Xunit;

namespace TeslaTracker.Domain.Tests.Orders;

public class OrderIdTests
{
    [Theory]
    [InlineData("RN123456789")]
    [InlineData("RN1234567890")]
    [InlineData("RN999999999")]
    public void Create_With_Valid_Rn_Number_Returns_Success(string raw)
    {
        var result = OrderId.Create(raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(raw);
    }

    [Fact]
    public void Create_Trims_Whitespace_Before_Validation()
    {
        var result = OrderId.Create("  RN123456789  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("RN123456789");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_With_Empty_Or_Null_Returns_Failure(string? raw)
    {
        var result = OrderId.Create(raw!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OrderId.Empty");
    }

    [Theory]
    [InlineData("RN")]
    [InlineData("RN12345")]
    [InlineData("RN12345678901")]
    [InlineData("RN12345678X")]
    [InlineData("ABC123456789")]
    [InlineData("rn123456789")]
    public void Create_With_Invalid_Format_Returns_Failure(string raw)
    {
        var result = OrderId.Create(raw);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OrderId.InvalidFormat");
    }

    [Fact]
    public void OrderId_Has_Value_Equality()
    {
        var a = OrderId.Create("RN123456789").Value;
        var b = OrderId.Create("RN123456789").Value;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ToString_Returns_Underlying_Value()
    {
        var id = OrderId.Create("RN123456789").Value;

        id.ToString().Should().Be("RN123456789");
    }
}
