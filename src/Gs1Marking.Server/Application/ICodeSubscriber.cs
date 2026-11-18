namespace Gs1Marking.Server.Application;

/// <summary>
/// Приёмник сгенерированных кодов. Реализуется транспортными подключениями
/// (например, отдельным TCP-клиентом).
/// </summary>
public interface ICodeSubscriber : IAsyncDisposable
{
    Guid Id { get; }

    /// <summary>Завершается, когда подписчик становится недоступен.</summary>
    Task Completion { get; }

    ValueTask PublishAsync(string code, CancellationToken cancellationToken = default);
}
