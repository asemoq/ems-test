namespace Gs1Marking.Contract.Gs1;

/// <summary>
/// Генерирует коды маркировки GS1 в человекочитаемой форме (HRI):
/// <c>(01)GTIN-14(21)SERIAL(17)EXPIRY(10)BATCH</c>.
/// </summary>
public sealed class Gs1CodeGenerator : IGs1CodeGenerator
{
    private const string SerialAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string BatchAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int SerialLength = 10;
    private const int BatchLength = 6;

    private readonly Random _random;
    private readonly TimeProvider _timeProvider;

    public Gs1CodeGenerator()
        : this(Random.Shared, TimeProvider.System)
    {
    }

    public Gs1CodeGenerator(Random random)
        : this(random, TimeProvider.System)
    {
    }

    public Gs1CodeGenerator(TimeProvider timeProvider)
        : this(Random.Shared, timeProvider)
    {
    }

    /// <summary>
    /// Создаёт генератор на основе конкретных экземпляров <see cref="Random"/> и
    /// <see cref="TimeProvider"/>, что позволяет детерминированно генерировать коды в тестах.
    /// </summary>
    public Gs1CodeGenerator(Random random, TimeProvider timeProvider)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string Generate()
    {
        var gtin14 = GenerateGtin14();
        var serial = GenerateRandom(SerialAlphabet, SerialLength);
        var expiryDate = GenerateExpiryDate();
        var batch = GenerateRandom(BatchAlphabet, BatchLength);

        return $"(01){gtin14}(21){serial}(17){expiryDate}(10){batch}";
    }

    private string GenerateGtin14()
    {
        Span<char> gtin = stackalloc char[14];

        // Индикатор упаковки: цифра 1..8.
        gtin[0] = (char)('0' + _random.Next(1, 9));
        for (var i = 1; i < 13; i++)
            gtin[i] = (char)('0' + _random.Next(0, 10));

        gtin[13] = Gs1CheckDigit.Compute(gtin[..13]);
        return new string(gtin);
    }

    private string GenerateExpiryDate()
    {
        // Срок годности: от 30 дней до 2 лет от текущей даты, формат YYMMDD.
        var expiry = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(_random.Next(30, 731));
        return expiry.ToString("yyMMdd");
    }

    private string GenerateRandom(string alphabet, int length)
    {
        return string.Create(length, (alphabet, _random), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = state.alphabet[state._random.Next(state.alphabet.Length)];
        });
    }
}
