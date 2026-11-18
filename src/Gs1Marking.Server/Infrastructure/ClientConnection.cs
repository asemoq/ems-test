using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Gs1Marking.Server.Application;
using Gs1Marking.Contract.Protocol;

namespace Gs1Marking.Server.Infrastructure;

/// <summary>
/// Представляет одно подключение клиента. Исходящий <see cref="Channel{T}"/>
/// отделяет цикл генерации от сети, а выделенная задача чтения определяет
/// отключение пира (чтение нуля байт означает, что пир закрыл соединение).
/// </summary>
internal sealed class ClientConnection : ICodeSubscriber
{
    /// <summary>Верхний предел исходящей очереди клиента; при отставании медленного клиента старые коды отбрасываются.</summary>
    private const int MaxOutboundQueue = 1024;

    private static readonly BoundedChannelOptions ChannelOptions = new(capacity: MaxOutboundQueue)
    {
        SingleWriter = true, SingleReader = true, AllowSynchronousContinuations = false, FullMode = BoundedChannelFullMode.DropOldest,
    };

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly Channel<string> _outbound = Channel.CreateBounded<string>(ChannelOptions);
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _writerTask;
    private readonly Task _readerTask;
    private int _disposed;

    private ClientConnection(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        Id = Guid.NewGuid();
        _writerTask = Task.Run(WriterLoopAsync);
        _readerTask = Task.Run(ReaderLoopAsync);
    }

    public Guid Id { get; }

    public EndPoint? RemoteEndPoint { get; private set; }

    /// <summary>
    /// Завершается при закрытии подключения (пиром или локально).
    /// </summary>
    public Task Completion => _completion.Task;

    public static ClientConnection FromAcceptedClient(TcpClient tcpClient)
    {
        var connection = new ClientConnection(tcpClient);
        connection.RemoteEndPoint = tcpClient.Client.RemoteEndPoint;
        return connection;
    }

    /// <summary>
    /// Ставит код в очередь на отправку. Никогда не блокируется; при переполнении
    /// очереди самый старый код отбрасывается, чтобы медленный клиент не рос в памяти безгранично.
    /// </summary>
    public ValueTask PublishAsync(string code, CancellationToken cancellationToken = default)
        => _outbound.Writer.WriteAsync(code, cancellationToken);

    private async Task WriterLoopAsync()
    {
        try
        {
            await foreach (var code in _outbound.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                await MessageFraming.WriteMessageAsync(_stream, code, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        { }
        catch (Exception)
        {
            // Ошибка уровня подключения (например, клиент исчез во время записи).
        }
        finally
        {
            SignalClosed();
        }
    }

    private async Task ReaderLoopAsync()
    {
        try
        {
            // Клиент никогда не отправляет данные; чтение нуля байт означает, что пир закрыл соединение.
            var buffer = new byte[64];
            while (true)
            {
                var read = await _stream.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                if (read == 0) break;
            }
        }
        catch (OperationCanceledException)
        { }
        catch (Exception)
        { }
        finally
        {
            SignalClosed();
        }
    }

    private void SignalClosed()
    {
        _cts.Cancel();
        _completion.TrySetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await _cts.CancelAsync();
        _outbound.Writer.TryComplete();
        await _stream.DisposeAsync();
        _tcpClient.Dispose();

        try
        {
            await Task.WhenAll(_writerTask, _readerTask).ConfigureAwait(false);
        }
        catch
        {
            // Циклы сами проглатывают свои исключения; эта защита чисто оборонительная.
        }

        _cts.Dispose();
    }
}
