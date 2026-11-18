using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Gs1Marking.Server.Application;
using Gs1Marking.Server.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gs1Marking.Server.Infrastructure;

/// <summary>
/// TCP-транспорт: принимает подключения и передаёт их жизненный цикл
/// broadcaster'у. Ничего не знает о GS1-кодах и политике генерации.
/// </summary>
public sealed partial class MarkingCodeServer : BackgroundService
{
    private readonly ServerOptions _options;
    private readonly IPAddress _bindAddress;
    private readonly IMarkingCodeBroadcaster _broadcaster;
    private readonly IClientConnectionFactory _connectionFactory;
    private readonly ILogger<MarkingCodeServer> _logger;
    private readonly ConcurrentDictionary<Guid, ICodeSubscriber> _clients = new();
    private readonly ConcurrentDictionary<Guid, Task> _handlers = new();
    private readonly TaskCompletionSource<IPEndPoint> _bound = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MarkingCodeServer(
        IOptions<ServerOptions> options,
        IMarkingCodeBroadcaster broadcaster,
        IClientConnectionFactory connectionFactory,
        ILogger<MarkingCodeServer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _bindAddress = IPAddress.Parse(_options.BindAddress);
        _broadcaster = broadcaster;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>Завершается после привязки слушателя, неся назначенный endpoint.</summary>
    public Task<IPEndPoint> Bound => _bound.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TcpListener listener;
        try
        {
            listener = new TcpListener(_bindAddress, _options.Port);
            listener.Start();
        }
        catch (Exception ex)
        {
            _bound.TrySetException(ex);
            throw;
        }

        var localEndpoint = (IPEndPoint)listener.LocalEndpoint;
        LogListening(_logger, localEndpoint);
        _bound.TrySetResult(localEndpoint);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = await listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                StartClient(tcpClient);
            }
        }
        finally
        {
            listener.Stop();

            foreach (var subscriber in _clients.Values)
            {
                await DisposeSubscriberAsync(subscriber).ConfigureAwait(false);
            }

            var handlers = _handlers.Values.ToArray();
            if (handlers.Length > 0)
            {
                await Task.WhenAll(handlers).ConfigureAwait(false);
            }

            _clients.Clear();
            _handlers.Clear();
        }
    }

    private void StartClient(TcpClient tcpClient)
    {
        ICodeSubscriber? subscriber = null;
        try
        {
            subscriber = _connectionFactory.Create(tcpClient);
            _broadcaster.Subscribe(subscriber);
            _clients[subscriber.Id] = subscriber;
            _handlers[subscriber.Id] = MonitorClientAsync(subscriber);
            LogClientConnected(_logger, tcpClient.Client.RemoteEndPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept client: {Message}", ex.Message);
            if (subscriber is not null)
            {
                _broadcaster.Unsubscribe(subscriber.Id);
                _clients.TryRemove(subscriber.Id, out _);
            }

            tcpClient.Dispose();
        }
    }

    private async Task MonitorClientAsync(ICodeSubscriber subscriber)
    {
        try
        {
            await subscriber.Completion.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Client handler error: {Message}", ex.Message);
        }
        finally
        {
            // Самоудаление удерживает оба словаря в пределах активных подключений.
            _broadcaster.Unsubscribe(subscriber.Id);
            _clients.TryRemove(subscriber.Id, out _);
            _handlers.TryRemove(subscriber.Id, out _);
            await DisposeSubscriberAsync(subscriber).ConfigureAwait(false);
            LogClientDisconnected(_logger, subscriber.Id);
        }
    }

    private async Task DisposeSubscriberAsync(ICodeSubscriber subscriber)
    {
        try
        {
            await subscriber.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispose subscriber {SubscriberId}.", subscriber.Id);
        }
    }

    [LoggerMessage(LogLevel.Information, "Listening on {Endpoint}")]
    private static partial void LogListening(ILogger logger, EndPoint endpoint);

    [LoggerMessage(LogLevel.Information, "Client connected: {RemoteEndPoint}")]
    private static partial void LogClientConnected(ILogger logger, EndPoint? remoteEndPoint);

    [LoggerMessage(LogLevel.Information, "Client disconnected: {SubscriberId}")]
    private static partial void LogClientDisconnected(ILogger logger, Guid subscriberId);
}
