namespace Gs1Marking.Contract.Gs1;

/// <summary>
/// Генерирует коды маркировки GS1.
/// </summary>
public interface IGs1CodeGenerator
{
    /// <summary>
    /// Генерирует один код маркировки GS1.
    /// </summary>
    string Generate();
}
