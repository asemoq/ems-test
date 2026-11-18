using System.Net.Sockets;
using Gs1Marking.Client.Application;
using Gs1Marking.Client.Options;
using Gs1Marking.Contract.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gs1Marking.Client.Infrastructure;

/// <summary>
/// TCP-клиент, подключающийся к серверу, принимающий коды маркировки GS1 и
/// дописывающий их в выходной файл. Автоматически переподключается при потере
/// соединения.
/// </summary>
public sealed partial class MarkingCodeClient(
    IOptions<ClientOptions> options,
    IMarkingCodeWriter writer,
    ILogger<MarkingCodeClient> logger) : BackgroundService
{
    private readonly ClientOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly IMarkingCodeWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly ILogger<MarkingCodeClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReceiveAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Connection failed: {Message}", ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Connection lost: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error: {Message}", ex.Message);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            LogReconnecting(_logger, _options.ReconnectDelay.TotalSeconds);
            try
            {
                await Task.Delay(_options.ReconnectDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndReceiveAsync(CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_options.Host, _options.Port, cancellationToken).ConfigureAwait(false);
        LogConnected(_logger, _options.Host, _options.Port);

        await using var stream = client.GetStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            var code = await MessageFraming.ReadMessageAsync(stream, cancellationToken).ConfigureAwait(false);
            await _writer.AppendAsync(code, cancellationToken).ConfigureAwait(false);
            LogReceived(_logger, code);
        }
    }

    [LoggerMessage(LogLevel.Information, "Reconnecting in {Delay}s...")]
    private static partial void LogReconnecting(ILogger logger, double delay);

    [LoggerMessage(LogLevel.Information, "Connected to {Host}:{Port}.")]
    private static partial void LogConnected(ILogger logger, string host, int port);

    [LoggerMessage(LogLevel.Information, "Received: {Code}")]
    private static partial void LogReceived(ILogger logger, string code);
}
