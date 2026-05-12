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
/// one-shot <see cref="QueryAsync(string, ClaudeAgentOptions?, CancellationToken)"/>
/// fast path, the streaming-input
/// <see cref="QueryAsync(IAsyncEnumerable{UserMessageInput}, ClaudeAgentOptions?, CancellationToken)"/>
/// overload, and surfaces <see cref="SdkVersion"/>. Mirrors the static
/// <c>query</c> function in the Python SDK (<c>claude_agent_sdk.query</c>).
/// </summary>
/// <remarks>
/// Hooks, in-process MCP, and the <c>CanUseTool</c> callback are wired
/// through the streaming control-protocol path on
/// <see cref="IClaudeAgentClient"/>; either the streaming-input overload
/// here or <see cref="ClaudeAgentClient"/> directly will route those
/// options correctly.
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
    /// <remarks>
    /// When <paramref name="options"/> requires the streaming control-protocol
    /// path — i.e. has a <see cref="ClaudeAgentOptions.CanUseTool"/>
    /// callback, registered hooks, or an in-process
    /// <see cref="Mcp.McpSdkServerConfig"/> — the call is transparently
    /// routed through <see cref="ClaudeAgentClient"/>. Otherwise the
    /// one-shot <c>--print</c> fast path is used.
    /// </remarks>
    public static async IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(prompt);
        options ??= new ClaudeAgentOptions();

        if (ControlProtocolGate.NeedsControlProtocol(options))
        {
            // Route the prompt through the streaming overload as a
            // single-element async sequence. The client owns the transport,
            // initialize handshake, EndInputAsync, and graceful shutdown.
            await foreach (
                var msg in QueryAsync(
                        SingleAsync(new UserMessageInput(prompt)),
                        options,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                yield return msg;
            }
            yield break;
        }

        var transport = new SubprocessCliTransport(options, oneShotPrompt: prompt);
        await foreach (
            var msg in QueryAsyncCore(options, transport, ownsTransport: true, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            yield return msg;
        }
    }

    /// <summary>
    /// Streaming-input query: runs a multi-turn streaming session against the
    /// CLI, pumping each <see cref="UserMessageInput"/> from
    /// <paramref name="prompts"/> into the conversation while concurrently
    /// yielding every <see cref="Message"/> the CLI emits in arrival order.
    /// Always uses the streaming control-protocol path. Mirrors the
    /// async-iterable prompt form of <c>claude_agent_sdk.query</c> in the
    /// Python SDK.
    /// </summary>
    /// <param name="prompts">User-turn sequence; the input stream's
    /// completion signals "no more input" to the CLI so it can finalise the
    /// result message and exit.</param>
    /// <param name="options">Session options. <see langword="null"/> uses
    /// defaults equivalent to <c>new ClaudeAgentOptions()</c>.</param>
    /// <param name="cancellationToken">Cancels both the input pump and the
    /// inbound message stream, then runs graceful shutdown of the child
    /// process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="prompts"/> is null.</exception>
    public static IAsyncEnumerable<Message> QueryAsync(
        IAsyncEnumerable<UserMessageInput> prompts,
        ClaudeAgentOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(prompts);
        options ??= new ClaudeAgentOptions();
        return QueryStreamingCore(prompts, options, transport: null, cancellationToken);
    }

    /// <summary>
    /// Test/integration seam for the streaming-input overload. Public
    /// <see cref="QueryAsync(IAsyncEnumerable{UserMessageInput}, ClaudeAgentOptions?, CancellationToken)"/>
    /// always passes <c>transport: null</c>; tests pass an in-process fake
    /// to avoid spawning a subprocess.
    /// </summary>
    internal static IAsyncEnumerable<Message> QueryAsync(
        IAsyncEnumerable<UserMessageInput> prompts,
        ClaudeAgentOptions options,
        ITransport transport,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);
        return QueryStreamingCore(prompts, options, transport, cancellationToken);
    }

    private static async IAsyncEnumerable<Message> QueryStreamingCore(
        IAsyncEnumerable<UserMessageInput> prompts,
        ClaudeAgentOptions options,
        ITransport? transport,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Construct the client with either a caller-supplied (test) transport
        // or a freshly-spawned subprocess transport that the client owns.
        var client = ClaudeAgentClient.CreateInternal(options, transport, loggerFactory: null);
        await using (client.ConfigureAwait(false))
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            // Pump the input stream concurrently with reading messages.
            // Closing input lets the CLI know the conversation is over and
            // it can finalise the result. Mirrors Python's
            // _internal/client.py:217-219 'spawn_task(stream_input(prompt))'.
            var pump = Task.Run(
                async () =>
                {
                    try
                    {
                        await foreach (
                            var input in prompts
                                .WithCancellation(cancellationToken)
                                .ConfigureAwait(false)
                        )
                        {
                            await client
                                .SendUserMessageAsync(input, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        // EndInputAsync must run on every exit path so the
                        // CLI can produce the result message. Use None so
                        // teardown still completes after cancellation.
                        try
                        {
                            await client
                                .EndInputAsync(CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            // The receive loop's exception (if any) takes
                            // precedence; pump errors surface only after
                            // the receive loop finishes cleanly.
                        }
                    }
                },
                cancellationToken
            );

            // yield-return cannot live inside try/catch, so we drive the
            // receive iterator manually. If the receive loop throws, that
            // exception propagates out of MoveNextAsync(); pump exceptions
            // surface only when the receive loop ended cleanly.
            await using var receive = client
                .ReceiveMessagesAsync(cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await receive.MoveNextAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Observe the pump task so it doesn't become an
                    // UnobservedTaskException, but let the receive error
                    // win — it's the more informative one.
                    _ = pump.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
                    throw;
                }

                if (!hasNext)
                {
                    break;
                }

                yield return receive.Current;
            }

            // Receive loop ended cleanly; pump errors (if any) surface here.
            await pump.ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<UserMessageInput> SingleAsync(UserMessageInput one)
    {
        yield return one;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Test/integration seam: runs the read loop against an arbitrary
    /// <see cref="ITransport"/>. The public
    /// <see cref="QueryAsync(string, ClaudeAgentOptions?, CancellationToken)"/>
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
