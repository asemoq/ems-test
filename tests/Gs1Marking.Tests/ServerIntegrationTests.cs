using System.Net;
using System.Net.Sockets;
using Gs1Marking.Server.Application;
using Gs1Marking.Server.Infrastructure;
using Gs1Marking.Server.Options;
using Gs1Marking.Contract.Gs1;
using Gs1Marking.Contract.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gs1Marking.Tests;

/// <summary>
/// Сквозной тест: сервер передаёт корректный GS1-код реальному TCP-клиенту.
/// </summary>
public class ServerIntegrationTests
{
    [Fact]
    public async Task Server_StreamsWellFormedGs1Code_ToConnectedClient()
    {
        var broadcaster = new MarkingCodeBroadcaster(
            new Gs1CodeGenerator(new Random(123)),
            Options.Create(new MarkingOptions { GenerationIntervalMs = 50 }),
            NullLogger<MarkingCodeBroadcaster>.Instance);

        var server = new MarkingCodeServer(
            Options.Create(new ServerOptions { BindAddress = IPAddress.Loopback.ToString(), Port = 0 }),
            broadcaster,
            new ClientConnectionFactory(),
            NullLogger<MarkingCodeServer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await server.StartAsync(cts.Token);
        await broadcaster.StartAsync(cts.Token);

        try
        {
            var endpoint = await server.Bound.WaitAsync(cts.Token);

            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Address, endpoint.Port, cts.Token);
            await using var stream = client.GetStream();

            var code = await MessageFraming.ReadMessageAsync(stream, cts.Token);

            Assert.Matches(@"^\(01\)\d{14}\(21\)[A-Z0-9]{10}\(17\)\d{6}\(10\)[A-Z0-9]{6}$", code);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
            await broadcaster.StopAsync(CancellationToken.None);
        }
    }
}
