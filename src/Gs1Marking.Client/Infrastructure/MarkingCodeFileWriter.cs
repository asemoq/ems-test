using System.Text;
using Gs1Marking.Client.Application;

namespace Gs1Marking.Client.Infrastructure;

/// <summary>
/// Дописывает полученные коды в файл по одному в строку. Файл открывается в режиме
/// добавления с <see cref="FileShare.Read"/>, чтобы другие процессы могли читать его
/// во время записи. Записи сериализуются и сразу сбрасываются на диск.
/// </summary>
public sealed class MarkingCodeFileWriter : IMarkingCodeWriter, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StreamWriter _writer;

    public MarkingCodeFileWriter(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 4096, useAsync: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    /// <summary>
    /// Дописывает один код новой строкой.
    /// </summary>
    public async Task AppendAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(code.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
