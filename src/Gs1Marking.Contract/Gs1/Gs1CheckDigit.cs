namespace Gs1Marking.Contract.Gs1;

/// <summary>
/// Вычисляет и проверяет контрольную цифру идентификаторов GS1 (GTIN-14).
/// </summary>
public static class Gs1CheckDigit
{
    /// <summary>
    /// Вычисляет контрольную цифру GS1 для переданных базовых цифр.
    /// </summary>
    /// <param name="digits">Базовые цифры (для GTIN-14 — первые 13 цифр).</param>
    /// <returns>Символ контрольной цифры.</returns>
    public static char Compute(ReadOnlySpan<char> digits)
    {
        if (digits.IsEmpty)
            throw new ArgumentException("Digits must not be empty.", nameof(digits));

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            if (digits[i] is < '0' or > '9')
                throw new ArgumentException("Digits must contain only numeric characters.", nameof(digits));

            var digit = digits[i] - '0';
            var weight = i % 2 == 0 ? 3 : 1;
            sum += digit * weight;
        }

        var checkDigit = (10 - sum % 10) % 10;
        return (char)('0' + checkDigit);
    }

    /// <summary>
    /// Проверяет полный 14-значный GTIN-14 вместе с контрольной цифрой.
    /// </summary>
    public static bool IsValid(ReadOnlySpan<char> gtin14)
    {
        if (gtin14.Length != 14)
            return false;

        for (var i = 0; i < gtin14.Length; i++)
        {
            if (gtin14[i] is < '0' or > '9')
                return false;
        }

        return Compute(gtin14[..13]) == gtin14[13];
    }
}
