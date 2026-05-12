using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Permissions;

namespace Daystrom.ClaudeAgentSdk;

/// <summary>
/// Bidirectional, streaming-mode client for the Claude Code CLI. Use this
/// type for multi-turn conversations, hook callbacks, in-process MCP tools,
/// and the <c>CanUseTool</c> permission callback. For one-shot
/// "ask once and read messages" flows prefer
/// <see cref="ClaudeAgent.QueryAsync(string, ClaudeAgentOptions?, CancellationToken)"/>.
/// Mirrors <c>claude_agent_sdk.client.ClaudeSDKClient</c>.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle: construct via
/// <see cref="ClaudeAgentClient.Create(ClaudeAgentOptions, Daystrom.ClaudeAgentSdk.Transport.ITransport?)"/>,
/// then call <see cref="ConnectAsync"/> exactly once before any other
/// member. The CLI handshake (initialize control request) runs as part of
/// connect, so subsequent control-protocol calls — <see cref="InterruptAsync"/>,
/// <see cref="GetMcpStatusAsync"/>, etc. — are valid only after
/// <see cref="ConnectAsync"/> resolves successfully.
/// </para>
/// <para>
/// Threading: all members are safe to invoke concurrently from multiple
/// threads. Outbound writes are serialised through a single internal
/// write lock (mirrors Python's <c>_write_lock</c>); the inbound message
/// stream returned by <see cref="ReceiveMessagesAsync"/> may only be
/// enumerated by a single consumer at a time.
/// </para>
/// <para>
/// Disposal: the client implements <see cref="IAsyncDisposable"/> and runs
/// the spec §9 graceful shutdown ladder when disposed (close stdin, await
/// process exit, kill if necessary). Always wrap usage in
/// <c>await using</c> — uncollected clients leave a zombie subprocess.
/// </para>
/// </remarks>
public interface IClaudeAgentClient : IAsyncDisposable
{
    /// <summary>
    /// Spawns the CLI subprocess (when no caller-supplied transport was
    /// provided), opens the stdio channel, and runs the <c>initialize</c>
    /// control-protocol handshake. Idempotent — a second call after a
    /// successful first one is a no-op.
    /// </summary>
    /// <param name="ct">Cancels the connect attempt and tears down any
    /// partially-started transport before throwing.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a plain-text user turn on the active session. Equivalent to
    /// <see cref="SendUserMessageAsync(UserMessageInput, CancellationToken)"/>
    /// with a <see cref="UserMessageInput"/> whose
    /// <see cref="UserMessageInput.Text"/> is <paramref name="text"/> and
    /// whose other fields are at their defaults.
    /// </summary>
    /// <param name="text">User-visible text. Must not be null.</param>
    /// <param name="ct">Cancels the write before bytes leave the SDK.</param>
    /// <exception cref="InvalidOperationException">Thrown when the session
    /// has already ended (stdin closed) or <see cref="ConnectAsync"/> was
    /// not called.</exception>
    Task SendUserMessageAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Sends a structured user turn (text plus optional session id and
    /// parent-tool-use id) on the active session. Mirrors the
    /// <c>{ "type": "user", "session_id", "message": { "role", "content" },
    /// "parent_tool_use_id" }</c> envelope the Python SDK writes.
    /// </summary>
    /// <param name="message">Wire envelope; must not be null.</param>
    /// <param name="ct">Cancels the write before bytes leave the SDK.</param>
    /// <exception cref="InvalidOperationException">Thrown when the session
    /// has already ended (stdin closed) or <see cref="ConnectAsync"/> was
    /// not called.</exception>
    Task SendUserMessageAsync(UserMessageInput message, CancellationToken ct = default);

    /// <summary>
    /// Yields every <see cref="Message"/> the CLI emits, in arrival order,
    /// until the subprocess closes its stdout. Control-protocol frames are
    /// consumed internally and never surface here; only "real" wire
    /// messages (assistant turns, results, system events, partial-message
    /// stream events) are yielded.
    /// </summary>
    /// <remarks>
    /// May only be enumerated by a single consumer. The iterator completes
    /// cleanly on a graceful shutdown; cancelling <paramref name="ct"/>
    /// surfaces an <see cref="OperationCanceledException"/>.
    /// </remarks>
    /// <param name="ct">Cancels iteration without disconnecting the
    /// underlying session.</param>
    IAsyncEnumerable<Message> ReceiveMessagesAsync(CancellationToken ct = default);

    /// <summary>
    /// Closes the SDK's input side (CLI's stdin) without tearing down the
    /// transport. Use after the final user turn to signal "no more input"
    /// so the CLI can compute the result message and exit cleanly. Mirrors
    /// closing stdin on the Python <c>SubprocessCLITransport</c>.
    /// </summary>
    Task EndInputAsync(CancellationToken ct = default);

    /// <summary>
    /// Tears down the active session: stops the read loop, disposes the
    /// control protocol, and (when the client owns the transport) runs the
    /// graceful-shutdown ladder on the CLI subprocess. Idempotent — safe
    /// to call from <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a <c>mcp_status</c> control request and returns the typed
    /// response describing every configured MCP server. Mirrors
    /// <c>ClaudeSDKClient.get_mcp_status</c>.
    /// </summary>
    Task<McpStatusResponse> GetMcpStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a <c>get_context_usage</c> control request and returns the
    /// typed response describing per-category token usage in the active
    /// session's context window. Mirrors
    /// <c>ClaudeSDKClient.get_context_usage</c>.
    /// </summary>
    Task<ContextUsageResponse> GetContextUsageAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies a permission update to the active session. Phase 6 only
    /// supports <c>setMode</c> updates (<see cref="PermissionUpdate.Type"/>
    /// equal to <c>"setMode"</c> with a non-null
    /// <see cref="PermissionUpdate.Mode"/>); rule-based and directory-based
    /// updates throw <see cref="NotSupportedException"/> until a later
    /// phase wires up the matching CLI control subtypes.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown for any update type
    /// other than <c>setMode</c>.</exception>
    Task ApplyPermissionUpdateAsync(PermissionUpdate update, CancellationToken ct = default);

    /// <summary>
    /// Sends an <c>interrupt</c> control request, asking the CLI to stop
    /// the current model invocation or tool execution. The subsequent
    /// <see cref="ReceiveMessagesAsync"/> stream surfaces a
    /// <see cref="ResultMessage"/> for the interrupted turn.
    /// </summary>
    Task InterruptAsync(CancellationToken ct = default);
}
