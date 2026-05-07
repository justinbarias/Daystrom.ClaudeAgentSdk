using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Daystrom.ClaudeAgentSdk.Transport;

/// <summary>
/// Public extension point for substituting how the SDK exchanges raw
/// stream-json messages with the Claude Code CLI (or any compatible
/// service). Mirrors <c>claude_agent_sdk._internal.transport.Transport</c>
/// in the Python SDK.
/// </summary>
/// <remarks>
/// <para>
/// This is a low-level I/O contract — message parsing and the control
/// protocol live above it. Implement to talk to a custom backend
/// (e.g. a remote CLI tunnelled over HTTP); for the standard subprocess
/// case use <see cref="SubprocessCliTransport"/>.
/// </para>
/// <para>
/// All operations support <see cref="CancellationToken"/>. Implementations
/// must be safe to dispose multiple times — <see cref="System.IAsyncDisposable.DisposeAsync"/>
/// is the cancellation-of-last-resort.
/// </para>
/// </remarks>
public interface ITransport : System.IAsyncDisposable
{
    /// <summary>True once <see cref="ConnectAsync"/> has run successfully and the channel is alive.</summary>
    bool IsReady { get; }

    /// <summary>
    /// Connect the transport and prepare it for I/O. For the subprocess
    /// transport, this spawns the child process. May throw
    /// <see cref="Errors.CliNotFoundException"/> /
    /// <see cref="Errors.CliConnectionException"/> on failure.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write a raw line of text to the transport's stdin (or equivalent).
    /// Implementations serialise concurrent writes through a single lock —
    /// callers may invoke from any thread.
    /// </summary>
    Task WriteAsync(string data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read parsed NDJSON messages from the transport's stdout (or
    /// equivalent), yielding one <see cref="JsonElement"/> per JSON
    /// document. The enumerable completes when the channel closes.
    /// </summary>
    IAsyncEnumerable<JsonElement> ReadMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Close the input side of the channel without tearing down the
    /// transport. Mirrors closing stdin on a child process.
    /// </summary>
    Task EndInputAsync(CancellationToken cancellationToken = default);
}
