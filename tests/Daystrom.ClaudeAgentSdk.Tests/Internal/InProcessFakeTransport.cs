using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Transport;

namespace Daystrom.ClaudeAgentSdk.Tests.Internal;

/// <summary>
/// Test-local in-process <see cref="ITransport"/> for control-protocol unit
/// tests. Tests push raw NDJSON lines onto <see cref="Inbound"/> via the
/// <c>Enqueue*</c> helpers and assert against <see cref="WrittenLines"/> for
/// outbound bytes the protocol wrote. Phase 13 will graduate a polished
/// version of this to a public <c>FakeTransport</c>; for now it stays
/// internal.
/// </summary>
internal sealed class InProcessFakeTransport : ITransport
{
    private readonly Channel<string> _inbound = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
    );
    private readonly object _writeLock = new();
    private readonly List<string> _written = new();
    private int _disposed;
    private bool _connected;
    private bool _stdinClosed;

    public ChannelWriter<string> Inbound => _inbound.Writer;

    public IReadOnlyList<string> WrittenLines
    {
        get
        {
            lock (_writeLock)
            {
                return _written.ToArray();
            }
        }
    }

    public bool IsReady => _connected && _disposed == 0;

    public bool StdinClosed => _stdinClosed;

    public bool Disposed => _disposed != 0;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    public Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_stdinClosed)
        {
            // Mirror SubprocessCliTransport: writing after stdin is closed
            // raises CliConnectionException (Phase 6.3 hardening).
            throw new Daystrom.ClaudeAgentSdk.Errors.CliConnectionException(
                "Cannot write: stdin has been closed."
            );
        }
        lock (_writeLock)
        {
            _written.Add(data);
        }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<JsonElement> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var reader = _inbound.Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var line))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                JsonElement element;
                using (var doc = JsonDocument.Parse(line))
                {
                    element = doc.RootElement.Clone();
                }
                yield return element;
            }
        }
    }

    public Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        _stdinClosed = true;
        _inbound.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }
        _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    public void EnqueueRaw(string line)
    {
        if (!_inbound.Writer.TryWrite(line))
        {
            throw new InvalidOperationException(
                "Failed to enqueue inbound line; channel is closed."
            );
        }
    }

    public void CompleteInbound()
    {
        _inbound.Writer.TryComplete();
    }

    /// <summary>
    /// Builds a <c>control_response</c> NDJSON line and pushes it onto the
    /// inbound channel. Either <paramref name="response"/> (success) or
    /// <paramref name="error"/> (error) should be set.
    /// </summary>
    public void EnqueueControlResponse(
        string requestId,
        string subtype,
        JsonElement? response = null,
        string? error = null
    )
    {
        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "control_response");
            writer.WriteStartObject("response");
            writer.WriteString("subtype", subtype);
            writer.WriteString("request_id", requestId);
            if (response is { } body)
            {
                writer.WritePropertyName("response");
                body.WriteTo(writer);
            }
            if (error is not null)
            {
                writer.WriteString("error", error);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        EnqueueRaw(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
    }

    public void EnqueueControlRequest(string requestId, string subtype, JsonElement? extras = null)
    {
        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "control_request");
            writer.WriteString("request_id", requestId);
            writer.WriteStartObject("request");
            writer.WriteString("subtype", subtype);
            if (extras is { } e && e.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in e.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        EnqueueRaw(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
    }

    public void EnqueueControlCancel(string requestId)
    {
        EnqueueRaw($"{{\"type\":\"control_cancel_request\",\"request_id\":\"{requestId}\"}}");
    }
}
