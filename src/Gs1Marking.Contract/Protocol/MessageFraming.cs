using System.Buffers.Binary;
using System.Text;

namespace Gs1Marking.Contract.Protocol;

/// <summary>
/// Фрейминг с префиксом длины для потоковых транспортов, таких как TCP.
///
/// Каждое сообщение кодируется как 4-байтовый big-endian префикс длины, за которым
/// следует UTF-8 payload. Чтение использует цикл «до полного заполнения буфера»,
/// что делает протокол устойчивым к частичным чтениям и предотвращает потерю или
/// «склеивание» сообщений.
/// </summary>
public static class MessageFraming
{
    private const int LengthPrefixSize = sizeof(int);

    /// <summary>
    /// Верхний предел размера одного сообщения. Защищает от неограниченных
    /// аллокаций при повреждённом префиксе длины.
    /// </summary>
    public const int MaxMessageLength = 1024 * 1024;

    /// <summary>
    /// Записывает одно обрамлённое сообщение в поток.
    /// </summary>
    public static async ValueTask WriteMessageAsync(Stream stream, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        var payload = Encoding.UTF8.GetBytes(message);
        var prefix = new byte[LengthPrefixSize];
        BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);

        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает одно обрамлённое сообщение из потока.
    /// </summary>
    /// <exception cref="EndOfStreamException">Поток завершился до того, как сообщение было прочитано полностью.</exception>
    /// <exception cref="InvalidDataException">Префикс длины выходит за допустимый диапазон.</exception>
    public static async ValueTask<string> ReadMessageAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var prefix = new byte[LengthPrefixSize];
        await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);

        var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
        if (length is < 0 or > MaxMessageLength)
            throw new InvalidDataException($"Message length {length} is outside the allowed range [0, {MaxMessageLength}].");

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(payload);
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("The connection was closed before a complete message could be read.");

            offset += read;
        }
    }
}
