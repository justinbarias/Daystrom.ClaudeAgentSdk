using System;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Transport;

namespace Daystrom.ClaudeAgentSdk.Internal;

/// <summary>
/// Decides whether a session can use the one-shot <c>--print</c> fast path
/// or must run through the streaming control protocol. Mirrors the
/// equivalent gating in <c>claude_agent_sdk._internal.client</c>.
/// </summary>
/// <remarks>
/// The fast path skips the control-protocol initialise handshake and the
/// bidirectional NDJSON exchange entirely; it is safe only when the
/// session has no in-process callbacks the CLI needs to round-trip back to
/// the SDK. Any of: a <see cref="ClaudeAgentOptions.CanUseTool"/>
/// callback, registered hooks, an <see cref="McpSdkServerConfig"/> in
/// <see cref="ClaudeAgentOptions.McpServers"/>, or a caller-supplied
/// custom <see cref="ITransport"/> forces the streaming path.
/// </remarks>
internal static class ControlProtocolGate
{
    /// <summary>
    /// Returns <see langword="true"/> when the session requires the
    /// streaming control-protocol path; <see langword="false"/> when the
    /// one-shot <c>--print</c> fast path is sufficient.
    /// </summary>
    public static bool NeedsControlProtocol(
        ClaudeAgentOptions options,
        ITransport? transport = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        if (transport is not null)
        {
            return true;
        }

        if (options.CanUseTool is not null)
        {
            return true;
        }

        if (options.Hooks is { Count: > 0 })
        {
            return true;
        }

        if (options.McpServers is { Count: > 0 } servers)
        {
            foreach (var config in servers.Values)
            {
                if (config is McpSdkServerConfig)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
