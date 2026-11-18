namespace Gs1Marking.Client.Application;

/// <summary>
/// Приёмник, сохраняющий полученные коды.
/// </summary>
public interface IMarkingCodeWriter
{
    Task AppendAsync(string code, CancellationToken cancellationToken = default);
}
