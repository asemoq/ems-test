using System.Collections.Concurrent;
using Gs1Marking.Server.Options;
using Gs1Marking.Contract.Gs1;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gs1Marking.Server.Application;

/// <summary>
/// Генерирует код маркировки GS1 с фиксированным интервалом и рассылает его всем
/// подписанным клиентам. Это прикладной/бизнес-слой: ничего не знает о TCP или
/// конкретном транспорте.
/// </summary>
public sealed partial class MarkingCodeBroadcaster(
    IGs1CodeGenerator generator,
    IOptions<MarkingOptions> options,
    ILogger<MarkingCodeBroadcaster> logger) : BackgroundService, IMarkingCodeBroadcaster
{
    private readonly IGs1CodeGenerator _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    private readonly MarkingOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly ILogger<MarkingCodeBroadcaster> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<Guid, ICodeSubscriber> _subscribers = new();

    public void Subscribe(ICodeSubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        _subscribers[subscriber.Id] = subscriber;
    }

    public void Unsubscribe(Guid id) => _subscribers.TryRemove(id, out _);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.GenerationInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                var code = _generator.Generate();
                LogGenerated(_logger, code);

                foreach (var subscriber in _subscribers.Values)
                {
                    try
                    {
                        await subscriber.PublishAsync(code, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Сбой одного подписчика не должен останавливать генерацию.
                        _logger.LogWarning(ex, "Failed to publish to subscriber {SubscriberId}.", subscriber.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Сервер завершает работу.
        }
    }

    [LoggerMessage(LogLevel.Information, "Generated: {Code}")]
    private static partial void LogGenerated(ILogger logger, string code);
}
