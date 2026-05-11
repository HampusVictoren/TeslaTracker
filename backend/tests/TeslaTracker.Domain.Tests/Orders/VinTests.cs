using FluentAssertions;
using TeslaTracker.Domain.Orders;
using Xunit;

namespace TeslaTracker.Domain.Tests.Orders;

public class VinTests
{
    [Theory]
    [InlineData("5YJYGDEE0LF000001")]
    [InlineData("7SAYGDEF1NF000123")]
    public void Create_Valid_Vin_Returns_Success(string raw)
    {
        var result = Vin.Create(raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(raw);
    }

    [Fact]
    public void Create_Lowercase_Vin_Normalizes_To_Upper()
    {
        var result = Vin.Create("5yjygdee0lf000001");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("5YJYGDEE0LF000001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Empty_Returns_Failure(string? raw)
    {
        var result = Vin.Create(raw!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Vin.Empty");
    }

    [Theory]
    [InlineData("SHORT")]
    [InlineData("12345678901234567890")]
    [InlineData("IIIIIIIIIIIIIIIII")]
    [InlineData("OOOOOOOOOOOOOOOOO")]
    [InlineData("QQQQQQQQQQQQQQQQQ")]
    public void Create_Invalid_Format_Returns_Failure(string raw)
    {
        var result = Vin.Create(raw);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Vin.InvalidFormat");
    }
}
