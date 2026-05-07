using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Transport;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Transport;

public class NdjsonReaderTests
{
    [Fact]
    public async Task ReadAsync_ParsesOneObjectPerLine()
    {
        var ndjson = "{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n";
        var elements = await ReadAllAsync(ndjson);

        Assert.Equal(3, elements.Count);
        Assert.Equal(1, elements[0].GetProperty("a").GetInt32());
        Assert.Equal(2, elements[1].GetProperty("b").GetInt32());
        Assert.Equal(3, elements[2].GetProperty("c").GetInt32());
    }

    [Fact]
    public async Task ReadAsync_HandlesArbitraryChunking()
    {
        // Same content, but split across reads at every byte.
        var ndjson = "{\"a\":1}\n{\"b\":2}\n";
        using var stream = new ByteAtATimeStream(Encoding.UTF8.GetBytes(ndjson));
        var reader = new NdjsonReader(stream);

        var elements = await Collect(reader);

        Assert.Equal(2, elements.Count);
        Assert.Equal(1, elements[0].GetProperty("a").GetInt32());
        Assert.Equal(2, elements[1].GetProperty("b").GetInt32());
    }

    [Fact]
    public async Task ReadAsync_HandlesObjectSplitAcrossMultipleLines()
    {
        // The CLI sometimes emits a single logical document broken across
        // physical newlines; the speculative buffer should reassemble it.
        var ndjson = "{\"a\":\n1,\"b\":\n2}\n";
        var elements = await ReadAllAsync(ndjson);

        Assert.Single(elements);
        Assert.Equal(1, elements[0].GetProperty("a").GetInt32());
        Assert.Equal(2, elements[0].GetProperty("b").GetInt32());
    }

    [Fact]
    public async Task ReadAsync_SkipsNonJsonPreamble()
    {
        // Warnings or banner lines from the CLI should be ignored, not
        // cause a parse failure.
        var ndjson =
            "[warning] starting up\n" + "claude version 2.0.0\n" + "{\"hello\":\"world\"}\n";
        var elements = await ReadAllAsync(ndjson);

        Assert.Single(elements);
        Assert.Equal("world", elements[0].GetProperty("hello").GetString());
    }

    [Fact]
    public async Task ReadAsync_ThrowsWhenBufferOverflows()
    {
        // Long unterminated JSON object will eventually exceed the buffer.
        var sb = new StringBuilder("{\"data\":\"");
        sb.Append('x', 200);
        // No closing quote / brace / newline — buffer keeps growing.
        var bytes = Encoding.UTF8.GetBytes(sb.ToString() + "\n");
        using var stream = new MemoryStream(bytes);
        var reader = new NdjsonReader(stream, maxBufferSize: 64);

        var ex = await Assert.ThrowsAsync<CliJsonDecodeException>(async () =>
        {
            await foreach (var _ in reader.ReadAsync())
            {
                // exhaust
            }
        });

        Assert.Contains("exceeded maximum size", ex.InnerException!.Message);
        Assert.NotEmpty(ex.RawLine);
    }

    [Fact]
    public async Task ReadAsync_RespectsCancellation()
    {
        // A blocking stream that never returns will let cancellation fire.
        using var stream = new BlockingStream();
        var reader = new NdjsonReader(stream);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in reader.ReadAsync(cts.Token))
            {
                // exhaust
            }
        });
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_YieldsNothing()
    {
        var elements = await ReadAllAsync(string.Empty);
        Assert.Empty(elements);
    }

    [Fact]
    public async Task ReadAsync_TolerantOfCrlf()
    {
        var ndjson = "{\"a\":1}\r\n{\"b\":2}\r\n";
        var elements = await ReadAllAsync(ndjson);

        Assert.Equal(2, elements.Count);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveBuffer()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => new NdjsonReader(stream, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NdjsonReader(stream, -1));
    }

    private static async Task<List<JsonElement>> ReadAllAsync(string ndjson)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
        var reader = new NdjsonReader(stream);
        return await Collect(reader);
    }

    private static async Task<List<JsonElement>> Collect(NdjsonReader reader)
    {
        var result = new List<JsonElement>();
        await foreach (var element in reader.ReadAsync())
        {
            result.Add(element);
        }
        return result;
    }

    /// <summary>
    /// Stream that returns at most one byte per Read call, simulating the
    /// pathological chunking pattern Python's TextReceiveStream guards
    /// against.
    /// </summary>
    private sealed class ByteAtATimeStream : Stream
    {
        private readonly byte[] _data;
        private int _pos;

        public ByteAtATimeStream(byte[] data)
        {
            _data = data;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position
        {
            get => _pos;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos >= _data.Length)
            {
                return 0;
            }
            buffer[offset] = _data[_pos++];
            return 1;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => Task.FromResult(Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            if (_pos >= _data.Length)
            {
                return ValueTask.FromResult(0);
            }
            buffer.Span[0] = _data[_pos++];
            return ValueTask.FromResult(1);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class BlockingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Block forever; cancellation is the way out.
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => 0);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
