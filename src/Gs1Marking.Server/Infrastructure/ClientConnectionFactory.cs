using System.Net.Sockets;
using Gs1Marking.Server.Application;

namespace Gs1Marking.Server.Infrastructure;

/// <summary>
/// Создаёт <see cref="ClientConnection"/> из принятого TCP-клиента.
/// </summary>
public sealed class ClientConnectionFactory : IClientConnectionFactory
{
    public ICodeSubscriber Create(TcpClient tcpClient) => ClientConnection.FromAcceptedClient(tcpClient);
}
