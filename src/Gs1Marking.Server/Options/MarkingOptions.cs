namespace Gs1Marking.Server.Options;

/// <summary>
/// Бизнес-конфигурация генерации и рассылки кодов.
/// </summary>
public sealed class MarkingOptions
{
    public const string SectionName = "Marking";

    /// <summary>Интервал генерации кода в миллисекундах.</summary>
    public int GenerationIntervalMs { get; set; } = 500;

    public TimeSpan GenerationInterval => TimeSpan.FromMilliseconds(GenerationIntervalMs);
}
