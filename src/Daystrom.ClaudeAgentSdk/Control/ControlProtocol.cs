using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Json;
using Daystrom.ClaudeAgentSdk.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daystrom.ClaudeAgentSdk.Control;

/// <summary>
/// Implements the SDK side of the Claude Code <c>stream-json</c> control
/// protocol on top of an <see cref="ITransport"/>. Mirrors Python's
/// <c>claude_agent_sdk._internal.query.Query</c> control-protocol concerns:
/// reads NDJSON from the transport, routes <c>control_response</c> /
/// <c>control_request</c> / <c>control_cancel_request</c> frames, forwards
/// everything else to a channel for the streaming-message consumer.
/// </summary>
/// <remarks>
/// <para>
/// All writes to <see cref="ITransport.WriteAsync"/> funnel through a single
/// <see cref="SemaphoreSlim"/> to mirror Python's <c>_write_lock</c>, so
/// concurrent <see cref="SendRequestAsync{TRequest, TResponse}"/> callers
/// can never interleave bytes on stdin.
/// </para>
/// <para>
/// Inbound <c>control_cancel_request</c> frames cancel the in-flight handler
/// task (matching <c>_internal/query.py:272–277</c>). When cancellation
/// fires, the SDK writes <em>no</em> response — late results are dropped.
/// </para>
/// <para>
/// Disposal cancels every in-flight handler, faults every pending response
/// TCS, completes the SDK-message channel, and waits up to ~5s for the read
/// loop to exit. The transport is owned by the caller and is NOT disposed.
/// </para>
/// </remarks>
internal sealed class ControlProtocol : IAsyncDisposable
{
    private const string StreamCloseTimeoutEnvKey = "CLAUDE_CODE_STREAM_CLOSE_TIMEOUT";
    private const int DefaultInitializeTimeoutMs = 60_000;

    private readonly ITransport _transport;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ResponseSlot> _pending = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlight = new();
    private readonly ConcurrentDictionary<
        Type,
        Func<ControlRequestPayload, CancellationToken, Task<JsonElement>>
    > _inboundHandlers = new();
    private readonly Channel<JsonElement> _sdkMessages = Channel.CreateUnbounded<JsonElement>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        }
    );
    private readonly CancellationTokenSource _lifetimeCts = new();
    private long _requestCounter;
    private Task? _readLoopTask;
    private int _readLoopStarted;
    private int _disposed;

    public ControlProtocol(ITransport transport, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Yields every NDJSON frame whose top-level <c>type</c> is not one of
    /// the control-protocol kinds. The caller (Phase 6.4 client) feeds these
    /// to <see cref="Internal.MessageParser"/>.
    /// </summary>
    public async IAsyncEnumerable<JsonElement> ReadSdkMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ThrowIfDisposed();
        EnsureReadLoopStarted();
        var reader = _sdkMessages.Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var element))
            {
                yield return element;
            }
        }
    }

    /// <summary>
    /// Sends an outbound <c>control_request</c>, awaits the matching
    /// <c>control_response</c>, and returns the typed body. Throws
    /// <see cref="ClaudeSdkException"/> on an error response,
    /// <see cref="TimeoutException"/> on timeout, and
    /// <see cref="OperationCanceledException"/> on cancellation/disposal.
    /// </summary>
    public async Task<TResponse?> SendRequestAsync<TRequest, TResponse>(
        TRequest payload,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
        where TRequest : ControlRequestPayload
        where TResponse : class
    {
        var body = await SendRequestCoreAsync(payload, timeout, cancellationToken)
            .ConfigureAwait(false);
        if (body is null || body.Value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse>)
            ClaudeAgentJsonContext.Default.GetTypeInfo(typeof(TResponse))!;
        return body.Value.Deserialize(typeInfo);
    }

    /// <summary>
    /// Sends an outbound <c>control_request</c> with no expected typed body
    /// (success body is ignored).
    /// </summary>
    public async Task SendRequestAsync<TRequest>(
        TRequest payload,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
        where TRequest : ControlRequestPayload
    {
        await SendRequestCoreAsync(payload, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a handler for inbound <c>control_request</c> frames whose
    /// payload deserialises to <typeparamref name="TRequest"/>. Returns a
    /// disposable that, when disposed, removes the registration. Phase 6
    /// ships scaffolding only — Phases 7/8/10 register the real handlers.
    /// </summary>
    public IDisposable RegisterInboundHandler<TRequest>(
        Func<TRequest, CancellationToken, Task<JsonElement>> handler
    )
        where TRequest : ControlRequestPayload
    {
        ArgumentNullException.ThrowIfNull(handler);
        var key = typeof(TRequest);
        Func<ControlRequestPayload, CancellationToken, Task<JsonElement>> wrapped = (req, ct) =>
            handler((TRequest)req, ct);
        if (!_inboundHandlers.TryAdd(key, wrapped))
        {
            throw new InvalidOperationException(
                $"An inbound handler for {key.Name} is already registered."
            );
        }
        return new HandlerRegistration(this, key);
    }

    /// <summary>
    /// Writes an already-serialised NDJSON line directly to the transport
    /// (e.g. user-message frames). Routes through the same write lock as
    /// control requests so it can never interleave with one.
    /// </summary>
    public async Task WriteRawAsync(
        string ndjsonLine,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(ndjsonLine);
        ThrowIfDisposed();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _transport.WriteAsync(ndjsonLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Reads <c>CLAUDE_CODE_STREAM_CLOSE_TIMEOUT</c> as a millisecond integer
    /// and returns a <see cref="TimeSpan"/>. Defaults to 60s when unset or
    /// unparseable; floors at 60s. Matches Python <c>_internal/client.py:162-165</c>.
    /// </summary>
    internal static TimeSpan ResolveInitializeTimeout()
    {
        var raw = Environment.GetEnvironmentVariable(StreamCloseTimeoutEnvKey);
        if (
            !string.IsNullOrEmpty(raw)
            && int.TryParse(raw, out var ms)
            && ms > DefaultInitializeTimeoutMs
        )
        {
            return TimeSpan.FromMilliseconds(ms);
        }
        return TimeSpan.FromMilliseconds(DefaultInitializeTimeoutMs);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _lifetimeCts.Cancel();
        }
        catch (Exception ex) when (IsSwallowable(ex))
        {
            _logger.LogDebug(ex, "Cancelling lifetime CTS threw; continuing.");
        }

        // Cancel all in-flight inbound handlers.
        foreach (var (_, cts) in _inFlight.ToArray())
        {
            try
            {
                cts.Cancel();
            }
            catch (Exception ex) when (IsSwallowable(ex))
            {
                _logger.LogDebug(ex, "Cancelling in-flight handler CTS threw; continuing.");
            }
        }

        // Fault all pending outbound requests so callers don't hang.
        FaultAllPending(new OperationCanceledException("ControlProtocol disposed."));

        // Make sure the SDK-message channel closes so consumers exit.
        _sdkMessages.Writer.TryComplete();

        if (_readLoopTask is not null)
        {
            try
            {
                var completed = await Task.WhenAny(
                        _readLoopTask,
                        Task.Delay(TimeSpan.FromSeconds(5))
                    )
                    .ConfigureAwait(false);
                if (completed != _readLoopTask)
                {
                    _logger.LogDebug("Read loop did not exit within 5s; orphaning.");
                }
            }
            catch (Exception ex) when (IsSwallowable(ex) || ex is OperationCanceledException)
            {
                // Already shutting down — swallow.
            }
        }

        _lifetimeCts.Dispose();
        _writeLock.Dispose();
    }

    // ── Internals ────────────────────────────────────────────────────────

    private async Task<JsonElement?> SendRequestCoreAsync(
        ControlRequestPayload payload,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(payload);
        ThrowIfDisposed();
        EnsureReadLoopStarted();

        var requestId = NewRequestId();
        var slot = new ResponseSlot();
        if (!_pending.TryAdd(requestId, slot))
        {
            throw new InvalidOperationException(
                $"Duplicate control request id generated: {requestId}."
            );
        }

        var envelope = new ControlRequestEnvelope { RequestId = requestId, Request = payload };
        var line =
            JsonSerializer.Serialize(
                envelope,
                ClaudeAgentJsonContext.Default.ControlRequestEnvelope
            ) + "\n";

        // Wire up timeout + cancellation. Both cancel the slot's TCS without
        // racing the read loop's success/failure path.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCts.Token
        );
        using var ctReg = linked.Token.Register(() => slot.Tcs.TrySetCanceled(linked.Token));
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenRegistration timeoutReg = default;
        if (timeout is { } t)
        {
            timeoutCts = new CancellationTokenSource(t);
            timeoutReg = timeoutCts.Token.Register(() =>
                slot.Tcs.TrySetException(
                    new TimeoutException(
                        $"Control request '{payload.GetType().Name}' (id {requestId}) timed out after {t.TotalSeconds:0.##}s."
                    )
                )
            );
        }

        try
        {
            await _writeLock.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                await _transport.WriteAsync(line, linked.Token).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            return await slot.Tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
            timeoutReg.Dispose();
            timeoutCts?.Dispose();
        }
    }

    private string NewRequestId()
    {
        var counter = Interlocked.Increment(ref _requestCounter);
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        Span<char> hex = stackalloc char[8];
        const string Lookup = "0123456789abcdef";
        for (var i = 0; i < bytes.Length; i++)
        {
            hex[i * 2] = Lookup[bytes[i] >> 4];
            hex[i * 2 + 1] = Lookup[bytes[i] & 0x0F];
        }
        return $"req_{counter}_{new string(hex)}";
    }

    private void EnsureReadLoopStarted()
    {
        if (Interlocked.Exchange(ref _readLoopStarted, 1) != 0)
        {
            return;
        }
        _readLoopTask = Task.Run(() => ReadLoopAsync(_lifetimeCts.Token));
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        Exception? terminalError = null;
        try
        {
            await foreach (
                var element in _transport.ReadMessagesAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (
                    element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("type", out var typeProp)
                    || typeProp.ValueKind != JsonValueKind.String
                )
                {
                    // Forward malformed-but-non-fatal frames downstream; the
                    // SDK message parser logs and skips. Mirrors Python's
                    // resilience pattern.
                    await _sdkMessages
                        .Writer.WriteAsync(element, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                var type = typeProp.GetString();
                switch (type)
                {
                    case "control_response":
                        DispatchControlResponse(element);
                        break;
                    case "control_request":
                        DispatchControlRequest(element);
                        break;
                    case "control_cancel_request":
                        DispatchControlCancel(element);
                        break;
                    default:
                        await _sdkMessages
                            .Writer.WriteAsync(element, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            terminalError = ex;
            _logger.LogError(ex, "Control protocol read loop failed.");
        }
        finally
        {
            // Cancel every in-flight inbound handler.
            foreach (var (_, cts) in _inFlight.ToArray())
            {
                try
                {
                    cts.Cancel();
                }
                catch (Exception ex) when (IsSwallowable(ex))
                {
                    _logger.LogDebug(ex, "Cancelling in-flight handler on read-loop exit threw.");
                }
            }

            FaultAllPending(
                terminalError is null
                    ? new OperationCanceledException("Control protocol read loop exited.")
                    : new ClaudeSdkException(
                        "Control protocol read loop failed: " + terminalError.Message,
                        terminalError
                    )
            );

            _sdkMessages.Writer.TryComplete(terminalError);
        }
    }

    private void DispatchControlResponse(JsonElement element)
    {
        ControlResponseEnvelope? envelope;
        try
        {
            envelope = element.Deserialize(ClaudeAgentJsonContext.Default.ControlResponseEnvelope);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed control_response frame; ignoring.");
            return;
        }
        if (envelope is null)
        {
            return;
        }

        var payload = envelope.Response;
        if (!_pending.TryGetValue(payload.RequestId, out var slot))
        {
            _logger.LogDebug(
                "control_response for unknown request id {RequestId}; ignoring.",
                payload.RequestId
            );
            return;
        }

        if (string.Equals(payload.Subtype, "error", StringComparison.Ordinal))
        {
            slot.Tcs.TrySetException(
                new ClaudeSdkException(
                    payload.Error ?? "Control request failed (no error message)."
                )
            );
        }
        else
        {
            slot.Tcs.TrySetResult(payload.Response);
        }
    }

    private void DispatchControlRequest(JsonElement element)
    {
        ControlRequestEnvelope? envelope;
        try
        {
            envelope = element.Deserialize(ClaudeAgentJsonContext.Default.ControlRequestEnvelope);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed inbound control_request frame; ignoring.");
            return;
        }
        if (envelope is null)
        {
            return;
        }

        var requestId = envelope.RequestId;
        var request = envelope.Request;
        var requestType = request.GetType();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        if (!_inFlight.TryAdd(requestId, cts))
        {
            _logger.LogDebug(
                "Duplicate inbound control_request id {RequestId}; dropping.",
                requestId
            );
            cts.Dispose();
            return;
        }

        _ = Task.Run(() => RunInboundHandlerAsync(requestId, request, requestType, cts));
    }

    private async Task RunInboundHandlerAsync(
        string requestId,
        ControlRequestPayload request,
        Type requestType,
        CancellationTokenSource cts
    )
    {
        try
        {
            if (!_inboundHandlers.TryGetValue(requestType, out var handler))
            {
                var subtype = ExtractSubtype(requestType);
                await WriteErrorResponseAsync(
                        requestId,
                        $"Unsupported control request subtype: {subtype}"
                    )
                    .ConfigureAwait(false);
                return;
            }

            JsonElement body;
            try
            {
                body = await handler(request, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Cancelled by control_cancel_request — drop silently. Matches
                // Python parity: no response is written for a cancelled
                // handler.
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Inbound control_request handler for {Type} (id {RequestId}) threw.",
                    requestType.Name,
                    requestId
                );
                await WriteErrorResponseAsync(requestId, ex.Message).ConfigureAwait(false);
                return;
            }

            if (cts.IsCancellationRequested)
            {
                return;
            }

            await WriteSuccessResponseAsync(requestId, body).ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(requestId, out _);
            cts.Dispose();
        }
    }

    private void DispatchControlCancel(JsonElement element)
    {
        ControlCancelRequestEnvelope? envelope;
        try
        {
            envelope = element.Deserialize(
                ClaudeAgentJsonContext.Default.ControlCancelRequestEnvelope
            );
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed control_cancel_request frame; ignoring.");
            return;
        }
        if (envelope is null)
        {
            return;
        }

        if (_inFlight.TryGetValue(envelope.RequestId, out var cts))
        {
            _logger.LogDebug(
                "Cancelling in-flight inbound control_request {RequestId}.",
                envelope.RequestId
            );
            try
            {
                cts.Cancel();
            }
            catch (Exception ex) when (IsSwallowable(ex))
            {
                _logger.LogDebug(ex, "Cancelling in-flight handler threw; continuing.");
            }
        }
    }

    private Task WriteSuccessResponseAsync(string requestId, JsonElement body)
    {
        var envelope = new ControlResponseEnvelope
        {
            Response = new ControlResponsePayload
            {
                Subtype = "success",
                RequestId = requestId,
                Response = body,
            },
        };
        return WriteEnvelopeAsync(envelope);
    }

    private Task WriteErrorResponseAsync(string requestId, string error)
    {
        var envelope = new ControlResponseEnvelope
        {
            Response = new ControlResponsePayload
            {
                Subtype = "error",
                RequestId = requestId,
                Error = error,
            },
        };
        return WriteEnvelopeAsync(envelope);
    }

    private async Task WriteEnvelopeAsync(ControlResponseEnvelope envelope)
    {
        var line =
            JsonSerializer.Serialize(
                envelope,
                ClaudeAgentJsonContext.Default.ControlResponseEnvelope
            ) + "\n";
        try
        {
            await _writeLock.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
            try
            {
                await _transport.WriteAsync(line, _lifetimeCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down — drop the response.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to write control_response envelope; continuing.");
        }
    }

    private void FaultAllPending(Exception cause)
    {
        foreach (var (id, slot) in _pending.ToArray())
        {
            if (cause is OperationCanceledException oce)
            {
                slot.Tcs.TrySetCanceled(oce.CancellationToken);
            }
            else
            {
                slot.Tcs.TrySetException(cause);
            }
            _pending.TryRemove(id, out _);
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(ControlProtocol));
        }
    }

    private static string ExtractSubtype(Type requestType)
    {
        // The [JsonDerivedType] discriminator is 'subtype'; for user-facing
        // diagnostics fall back to the .NET type name (CanUseToolRequest →
        // can_use_tool) when the attribute lookup is missing.
        foreach (
            var attr in typeof(ControlRequestPayload).GetCustomAttributes(
                typeof(System.Text.Json.Serialization.JsonDerivedTypeAttribute),
                inherit: false
            )
        )
        {
            if (
                attr is System.Text.Json.Serialization.JsonDerivedTypeAttribute derived
                && derived.DerivedType == requestType
                && derived.TypeDiscriminator is string s
            )
            {
                return s;
            }
        }
        var name = requestType.Name;
        if (name.EndsWith("Request", StringComparison.Ordinal))
        {
            name = name[..^"Request".Length];
        }
        return name;
    }

    private static bool IsSwallowable(Exception ex) =>
        ex is ObjectDisposedException or InvalidOperationException;

    private sealed class ResponseSlot
    {
        public TaskCompletionSource<JsonElement?> Tcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long StartedAtTicks { get; } = Stopwatch.GetTimestamp();
    }

    private sealed class HandlerRegistration : IDisposable
    {
        private readonly ControlProtocol _owner;
        private readonly Type _key;
        private int _disposed;

        public HandlerRegistration(ControlProtocol owner, Type key)
        {
            _owner = owner;
            _key = key;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            _owner._inboundHandlers.TryRemove(_key, out _);
        }
    }
}
