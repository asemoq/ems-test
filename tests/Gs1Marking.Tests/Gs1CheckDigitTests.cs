using Gs1Marking.Contract.Gs1;

namespace Gs1Marking.Tests;

public class Gs1CheckDigitTests
{
    [Theory]
    [InlineData("0001234567890", '5')]
    [InlineData("1001234567890", '2')]
    public void Compute_ReturnsExpectedCheckDigit(string digits, char expected)
    {
        Assert.Equal(expected, Gs1CheckDigit.Compute(digits));
    }

    [Theory]
    [InlineData("00012345678905", true)]
    [InlineData("10012345678902", true)]
    [InlineData("00012345678906", false)]
    [InlineData("0001234567890", false)]   // слишком короткий
    [InlineData("000123456789051", false)] // слишком длинный
    [InlineData("0001234567890X", false)]  // нечисловой
    public void IsValid_ReturnsExpectedResult(string gtin, bool expected)
    {
        Assert.Equal(expected, Gs1CheckDigit.IsValid(gtin));
    }

    [Fact]
    public void Compute_ThrowsOnEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => Gs1CheckDigit.Compute(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void Compute_ThrowsOnNonNumericInput()
    {
        Assert.Throws<ArgumentException>(() => Gs1CheckDigit.Compute("000123456789A"));
    }
}
