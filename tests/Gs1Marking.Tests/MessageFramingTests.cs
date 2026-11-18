using System.Text;
using Gs1Marking.Contract.Protocol;

namespace Gs1Marking.Tests;

public class MessageFramingTests
{
    [Fact]
    public async Task RoundTrip_SingleMessage()
    {
        using var stream = new MemoryStream();

        await MessageFraming.WriteMessageAsync(stream, "hello");
        stream.Position = 0;

        var result = await MessageFraming.ReadMessageAsync(stream);

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task RoundTrip_MultipleMessages_DoNotMergeOrSplit()
    {
        using var stream = new MemoryStream();
        string[] messages = { "first", "second", "третий", "😀🚀", string.Empty };

        foreach (var message in messages)
            await MessageFraming.WriteMessageAsync(stream, message);

        stream.Position = 0;

        foreach (var message in messages)
        {
            var result = await MessageFraming.ReadMessageAsync(stream);
            Assert.Equal(message, result);
        }

        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public async Task Read_HandlesPartialReads()
    {
        using var stream = new MemoryStream();
        await MessageFraming.WriteMessageAsync(stream, "partial-read-message");

        using var chunked = new ChunkedReadStream(stream.ToArray(), maxChunkSize: 3);

        var result = await MessageFraming.ReadMessageAsync(chunked);

        Assert.Equal("partial-read-message", result);
    }

    [Fact]
    public async Task Read_ThrowsOnTruncatedMessage()
    {
        using var stream = new MemoryStream();
        await MessageFraming.WriteMessageAsync(stream, "complete");
        var data = stream.ToArray();

        using var truncated = new MemoryStream(data, 0, data.Length - 3);

        await Assert.ThrowsAsync<EndOfStreamException>(() => MessageFraming.ReadMessageAsync(truncated).AsTask());
    }

    [Fact]
    public async Task Read_ThrowsOnInvalidLength()
    {
        using var stream = new MemoryStream();
        byte[] prefix = { 0xFF, 0xFF, 0xFF, 0xFF }; // -1 как знаковое целое
        await stream.WriteAsync(prefix);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => MessageFraming.ReadMessageAsync(stream).AsTask());
    }

    private sealed class ChunkedReadStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _maxChunkSize;
        private int _position;

        public ChunkedReadStream(byte[] data, int maxChunkSize)
        {
            _data = data;
            _maxChunkSize = maxChunkSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var toRead = Math.Min(count, Math.Min(_maxChunkSize, _data.Length - _position));
            if (toRead <= 0)
                return 0;

            Array.Copy(_data, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
