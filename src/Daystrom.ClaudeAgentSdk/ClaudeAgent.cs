using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Internal;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Transport;

namespace Daystrom.ClaudeAgentSdk;

/// <summary>
/// Top-level static entry point for the Claude Agent SDK. Hosts the
/// one-shot <see cref="QueryAsync"/> fast path and surfaces
/// <see cref="SdkVersion"/>. Mirrors the static <c>query</c> function in
/// the Python SDK (<c>claude_agent_sdk.query</c>).
/// </summary>
/// <remarks>
/// Multi-turn streaming, hooks, in-process MCP, and the
/// <c>CanUseTool</c> callback live on <c>IClaudeAgentClient</c> (Phase 6+);
/// this class deliberately exposes only the primitive
/// "ask once, read messages, exit" flow.
/// </remarks>
public static class ClaudeAgent
{
    /// <summary>
    /// SDK version string surfaced both at runtime and on the wire via the
    /// <c>CLAUDE_AGENT_SDK_VERSION</c> environment variable. Sourced from
    /// the assembly's
    /// <see cref="AssemblyInformationalVersionAttribute"/>, falling back to
    /// the assembly version, with a final fallback of <c>"0.0.0"</c>.
    /// </summary>
    public static string SdkVersion { get; } = ResolveSdkVersion();

    private static string ResolveSdkVersion()
    {
        var assembly = typeof(ClaudeAgent).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            // Strip the "+<sha>" build-metadata suffix MSBuild sometimes
            // appends — callers want the SemVer core.
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    /// <summary>
    /// Runs a one-shot query against the bundled Claude Code CLI in
    /// <c>--print</c> mode and yields each <see cref="Message"/> the CLI
    /// emits, in order, until the subprocess exits.
    /// </summary>
    /// <param name="prompt">Prompt sent to the CLI. Must be non-null.</param>
    /// <param name="options">
    /// Session options. <see langword="null"/> uses defaults equivalent to
    /// <c>new ClaudeAgentOptions()</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the read loop and triggers graceful shutdown of the child
    /// process (close stdin → wait → kill → wait → kill tree).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="options"/> requires the streaming
    /// control-protocol path — i.e. has a
    /// <see cref="ClaudeAgentOptions.CanUseTool"/> callback, registered
    /// hooks, or an in-process
    /// <see cref="Mcp.McpSdkServerConfig"/>. Streaming support lands in
    /// Phase 6 via <c>IClaudeAgentClient</c>; until then, those options
    /// have no implementation behind them and the SDK refuses to silently
    /// drop them.
    /// </exception>
    public static IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(prompt);
        options ??= new ClaudeAgentOptions();

        if (ControlProtocolGate.NeedsControlProtocol(options))
        {
            throw new NotSupportedException(
                "Hooks, CanUseTool, and in-process MCP servers require the "
                    + "streaming control-protocol path which lands in Phase 6. Use "
                    + "ClaudeAgentClient (forthcoming) instead."
            );
        }

        var transport = new SubprocessCliTransport(options, oneShotPrompt: prompt);
        return QueryAsyncCore(options, transport, ownsTransport: true, cancellationToken);
    }

    /// <summary>
    /// Test/integration seam: runs the read loop against an arbitrary
    /// <see cref="ITransport"/>. The public <see cref="QueryAsync"/>
    /// always passes a freshly-constructed
    /// <see cref="SubprocessCliTransport"/> with <c>ownsTransport: true</c>;
    /// the testing package (Phase 13) drives this overload with a
    /// <c>FakeTransport</c>.
    /// </summary>
    internal static async IAsyncEnumerable<Message> QueryAsyncCore(
        ClaudeAgentOptions options,
        ITransport transport,
        bool ownsTransport,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);

        try
        {
            await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);

            await foreach (
                var element in transport.ReadMessagesAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                var message = MessageParser.Parse(element);
                if (message is not null)
                {
                    yield return message;
                }
            }
        }
        finally
        {
            if (ownsTransport)
            {
                await transport.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
