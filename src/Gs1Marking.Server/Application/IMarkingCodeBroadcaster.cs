namespace Gs1Marking.Server.Application;

/// <summary>
/// Генерирует коды с фиксированным интервалом и рассылает их подписчикам.
/// </summary>
public interface IMarkingCodeBroadcaster
{
    void Subscribe(ICodeSubscriber subscriber);

    void Unsubscribe(Guid id);
}
