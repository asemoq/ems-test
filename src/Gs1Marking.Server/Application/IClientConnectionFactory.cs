using System.Net.Sockets;

namespace Gs1Marking.Server.Application;

/// <summary>
/// Создаёт подписчика из принятого TCP-клиента. Отделяет цикл приёма подключений
/// от конкретного типа <c>ClientConnection</c>, чтобы транспорт можно было тестировать
/// с fake-подписчиками.
/// </summary>
public interface IClientConnectionFactory
{
    ICodeSubscriber Create(TcpClient tcpClient);
}
