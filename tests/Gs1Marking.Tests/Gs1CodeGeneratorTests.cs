using System.Text.RegularExpressions;
using Gs1Marking.Contract.Gs1;

namespace Gs1Marking.Tests;

public class Gs1CodeGeneratorTests
{
    private const string FormatPattern = @"^\(01\)\d{14}\(21\)[A-Z0-9]{10}\(17\)\d{6}\(10\)[A-Z0-9]{6}$";

    [Fact]
    public void Generate_ProducesWellFormedGs1Code()
    {
        var generator = new Gs1CodeGenerator(new Random(12345));
        var code = generator.Generate();

        Assert.Matches(FormatPattern, code);
    }

    [Fact]
    public void Generate_GtinHasValidCheckDigit()
    {
        var generator = new Gs1CodeGenerator(new Random(42));

        for (var i = 0; i < 200; i++)
        {
            var code = generator.Generate();
            var gtin = ExtractAiContent(code, "01");

            Assert.True(Gs1CheckDigit.IsValid(gtin), $"Invalid GTIN: {gtin}");
        }
    }

    [Fact]
    public void Generate_ProducesUniqueCodes()
    {
        var generator = new Gs1CodeGenerator(new Random(7));

        var codes = Enumerable.Range(0, 1000)
            .Select(_ => generator.Generate())
            .ToHashSet();

        Assert.Equal(1000, codes.Count);
    }

    [Fact]
    public void Generate_ExpiryDate_IsWithinExpectedRange()
    {
        var fixedTime = new FixedTimeProvider(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        var generator = new Gs1CodeGenerator(new Random(1), fixedTime);

        var code = generator.Generate();
        var expiry = ExtractAiContent(code, "17");

        var expiryDate = DateOnly.ParseExact(expiry, "yyMMdd");
        var today = DateOnly.FromDateTime(fixedTime.GetUtcNow().UtcDateTime);
        var days = expiryDate.DayNumber - today.DayNumber;

        Assert.InRange(days, 30, 730);
    }

    private static string ExtractAiContent(string code, string ai)
    {
        var match = Regex.Match(code, $@"\({ai}\)([^()]+)");
        Assert.True(match.Success, $"AI ({ai}) not found in code: {code}");
        return match.Groups[1].Value;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
