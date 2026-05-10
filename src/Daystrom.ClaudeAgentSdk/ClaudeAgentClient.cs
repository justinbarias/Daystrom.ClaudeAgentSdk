using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Control;
using Daystrom.ClaudeAgentSdk.Control.Requests;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Internal;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.SystemPrompt;
using Daystrom.ClaudeAgentSdk.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daystrom.ClaudeAgentSdk;

/// <summary>
/// Default <see cref="IClaudeAgentClient"/> implementation. Owns the
/// <see cref="ControlProtocol"/>, drives the <c>initialize</c> handshake on
/// connect, and routes outbound user turns plus typed control requests
/// onto the transport. Mirrors
/// <c>claude_agent_sdk.client.ClaudeSDKClient</c>.
/// </summary>
/// <remarks>
/// <para>
/// Construction goes through <see cref="Create"/> rather than a public
/// constructor so the "owns the transport when none was supplied" rule
/// stays inside the class. Tests pass a fake transport explicitly and the
/// client never disposes it.
/// </para>
/// </remarks>
public sealed class ClaudeAgentClient : IClaudeAgentClient
{
    private readonly ClaudeAgentOptions _options;
    private readonly ITransport _transport;
    private readonly bool _ownsTransport;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private ControlProtocol? _protocol;
    private bool _connected;
    private bool _disposed;
    private bool _stdinClosed;

    private ClaudeAgentClient(
        ClaudeAgentOptions options,
        ITransport transport,
        bool ownsTransport,
        ILoggerFactory? loggerFactory
    )
    {
        _options = options;
        _transport = transport;
        _ownsTransport = ownsTransport;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<ClaudeAgentClient>();
    }

    /// <summary>
    /// Builds a streaming client. When <paramref name="transport"/> is
    /// <see langword="null"/>, a fresh <see cref="SubprocessCliTransport"/>
    /// is constructed in streaming mode and owned by the returned client
    /// (it is disposed on <see cref="DisconnectAsync"/> /
    /// <see cref="DisposeAsync"/>). Pass a transport explicitly for tests
    /// or custom backends — the caller retains ownership in that case.
    /// </summary>
    public static IClaudeAgentClient Create(
        ClaudeAgentOptions options,
        ITransport? transport = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateInternal(options, transport, loggerFactory: null);
    }

    /// <summary>
    /// Test-only seam: build a client around a caller-supplied transport
    /// while still asserting ownership. Used by unit tests that need to
    /// verify the "dispose owned transport on initialize failure" path
    /// without spawning a real subprocess.
    /// </summary>
    internal static ClaudeAgentClient CreateForTest(
        ClaudeAgentOptions options,
        ITransport transport,
        bool ownsTransport,
        ILoggerFactory? loggerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);
        return new ClaudeAgentClient(options, transport, ownsTransport, loggerFactory);
    }

    internal static ClaudeAgentClient CreateInternal(
        ClaudeAgentOptions options,
        ITransport? transport,
        ILoggerFactory? loggerFactory
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ITransport effectiveTransport;
        bool owns;
        if (transport is null)
        {
            // Streaming mode: oneShotPrompt is null. Mirrors the Phase 5
            // construction site in ClaudeAgent.QueryAsync; we deliberately
            // do not stuff a prompt onto argv here — the streaming client
            // pushes user turns through the control-protocol write lock.
            effectiveTransport = new SubprocessCliTransport(
                options,
                oneShotPrompt: null,
                logger: loggerFactory?.CreateLogger<SubprocessCliTransport>()
            );
            owns = true;
        }
        else
        {
            effectiveTransport = transport;
            owns = false;
        }
        return new ClaudeAgentClient(options, effectiveTransport, owns, loggerFactory);
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_connected)
            {
                return;
            }

            await _transport.ConnectAsync(ct).ConfigureAwait(false);

            var protocol = new ControlProtocol(
                _transport,
                _loggerFactory.CreateLogger<ControlProtocol>()
            );

            try
            {
                var initRequest = BuildInitializeRequest();
                await protocol
                    .SendRequestAsync<InitializeRequest>(
                        initRequest,
                        timeout: ControlProtocol.ResolveInitializeTimeout(),
                        cancellationToken: ct
                    )
                    .ConfigureAwait(false);
            }
            catch
            {
                await protocol.DisposeAsync().ConfigureAwait(false);
                if (_ownsTransport)
                {
                    try
                    {
                        await _transport.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(
                            ex,
                            "Disposing owned transport after failed initialize threw; swallowing."
                        );
                    }
                }
                throw;
            }

            _protocol = protocol;
            _connected = true;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private InitializeRequest BuildInitializeRequest()
    {
        bool? excludeDynamicSections = null;
        if (
            _options.SystemPrompt is SystemPromptPreset preset
            && preset.ExcludeDynamicSections is bool eds
        )
        {
            excludeDynamicSections = eds;
        }

        // Phase 6 ships hooks/agents as null; Phases 7/9 will populate them
        // from _options once the matching wire types land.
        return new InitializeRequest
        {
            Hooks = null,
            Agents = null,
            ExcludeDynamicSections = excludeDynamicSections,
            Skills = null,
        };
    }

    /// <inheritdoc />
    public Task SendUserMessageAsync(string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return SendUserMessageAsync(new UserMessageInput(text), ct);
    }

    /// <inheritdoc />
    public async Task SendUserMessageAsync(UserMessageInput message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        var line = message.ToWireJson() + "\n";
        try
        {
            await _protocol!.WriteRawAsync(line, ct).ConfigureAwait(false);
        }
        catch (CliConnectionException ex) when (_stdinClosed || IsStdinClosedError(ex))
        {
            throw new InvalidOperationException(
                "Cannot send a user message: the agent session has ended.",
                ex
            );
        }
    }

    private static bool IsStdinClosedError(CliConnectionException ex) =>
        ex.InnerException
            is System.IO.IOException
                or ObjectDisposedException
                or InvalidOperationException;

    /// <inheritdoc />
    public async IAsyncEnumerable<Message> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        await foreach (var element in _protocol!.ReadSdkMessagesAsync(ct).ConfigureAwait(false))
        {
            var parsed = MessageParser.Parse(element);
            if (parsed is not null)
            {
                yield return parsed;
            }
        }
    }

    /// <inheritdoc />
    public async Task EndInputAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        try
        {
            await _transport.EndInputAsync(ct).ConfigureAwait(false);
            _stdinClosed = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "EndInputAsync on transport threw; rethrowing.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_connected)
            {
                return;
            }

            // Best-effort: close stdin to signal the CLI we're done so the
            // read loop drains naturally, then dispose the protocol.
            if (!_stdinClosed)
            {
                try
                {
                    await _transport.EndInputAsync(ct).ConfigureAwait(false);
                    _stdinClosed = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "EndInputAsync during disconnect threw; continuing.");
                }
            }

            if (_protocol is not null)
            {
                try
                {
                    await _protocol.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "Disposing ControlProtocol during disconnect threw.");
                }
                _protocol = null;
            }

            if (_ownsTransport)
            {
                try
                {
                    await _transport.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "Disposing owned transport during disconnect threw.");
                }
            }

            _connected = false;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<McpStatusResponse> GetMcpStatusAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        var response = await _protocol!
            .SendRequestAsync<McpStatusRequest, McpStatusResponse>(
                new McpStatusRequest(),
                timeout: null,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
        if (response is null)
        {
            throw new ClaudeSdkException(
                "CLI returned a successful control response with no body for mcp_status."
            );
        }
        return response;
    }

    /// <inheritdoc />
    public async Task<ContextUsageResponse> GetContextUsageAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        var response = await _protocol!
            .SendRequestAsync<GetContextUsageRequest, ContextUsageResponse>(
                new GetContextUsageRequest(),
                timeout: null,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
        if (response is null)
        {
            throw new ClaudeSdkException(
                "CLI returned a successful control response with no body for get_context_usage."
            );
        }
        return response;
    }

    /// <inheritdoc />
    public async Task ApplyPermissionUpdateAsync(
        PermissionUpdate update,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(update);
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        if (
            !string.Equals(update.Type, "setMode", StringComparison.Ordinal)
            || update.Mode is not PermissionMode mode
        )
        {
            throw new NotSupportedException(
                "Phase 6 only supports PermissionMode updates "
                    + $"(update.Type=\"setMode\", update.Mode != null); received "
                    + $"Type=\"{update.Type}\". Rule and directory updates ship in a later phase."
            );
        }

        await _protocol!
            .SendRequestAsync<SetPermissionModeRequest>(
                new SetPermissionModeRequest { Mode = mode },
                timeout: null,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InterruptAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnected();

        await _protocol!
            .SendRequestAsync<InterruptRequest>(
                new InterruptRequest(),
                timeout: null,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // Reuse DisconnectAsync's lifecycle so dispose on a never-connected
        // client is still cheap and idempotent. Pass CancellationToken.None
        // — disposal must always run to completion.
        try
        {
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "DisconnectAsync during DisposeAsync threw; swallowing.");
        }

        // If Connect never ran, _ownsTransport may still hold an
        // unconnected transport that needs disposal.
        if (!_connected && _ownsTransport && _protocol is null)
        {
            try
            {
                await _transport.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(
                    ex,
                    "Disposing unconnected owned transport on DisposeAsync threw."
                );
            }
        }

        _lifecycleLock.Dispose();
    }

    private void EnsureConnected()
    {
        if (!_connected || _protocol is null)
        {
            throw new InvalidOperationException(
                "ClaudeAgentClient is not connected. Call ConnectAsync first."
            );
        }
    }
}
