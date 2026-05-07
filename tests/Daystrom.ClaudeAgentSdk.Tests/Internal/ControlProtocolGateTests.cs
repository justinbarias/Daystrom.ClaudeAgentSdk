using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Hooks;
using Daystrom.ClaudeAgentSdk.Internal;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Transport;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Internal;

public class ControlProtocolGateTests
{
    [Fact]
    public void DefaultOptions_DoNotNeedControlProtocol()
    {
        var options = new ClaudeAgentOptions();
        Assert.False(ControlProtocolGate.NeedsControlProtocol(options));
    }

    [Fact]
    public void CanUseTool_ForcesControlProtocol()
    {
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (name, input, ctx, ct) =>
                ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
        };
        Assert.True(ControlProtocolGate.NeedsControlProtocol(options));
    }

    [Fact]
    public void NonEmptyHooks_ForceControlProtocol()
    {
        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = new[] { new HookMatcher { Matcher = "Bash" } },
            },
        };
        Assert.True(ControlProtocolGate.NeedsControlProtocol(options));
    }

    [Fact]
    public void EmptyHooksDictionary_DoesNotForceControlProtocol()
    {
        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>(),
        };
        Assert.False(ControlProtocolGate.NeedsControlProtocol(options));
    }

    [Fact]
    public void McpSdkServerConfig_ForcesControlProtocol()
    {
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["calc"] = new McpSdkServerConfig { Name = "calc", Instance = null! },
            },
        };
        Assert.True(ControlProtocolGate.NeedsControlProtocol(options));
    }

    [Fact]
    public void NonSdkMcpServerConfigs_DoNotForceControlProtocol()
    {
        // Stdio/SSE/HTTP MCP servers are spawned by the CLI itself and
        // never round-trip back to the SDK process, so they are compatible
        // with the one-shot fast path.
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["docs"] = new McpStdioServerConfig
                {
                    Command = "/usr/bin/some-mcp-server",
                    Args = new[] { "--port", "0" },
                },
            },
        };
        Assert.False(ControlProtocolGate.NeedsControlProtocol(options));
    }

    [Fact]
    public void CustomTransport_ForcesControlProtocol()
    {
        var options = new ClaudeAgentOptions();
        var transport = new NullTransport();
        Assert.True(ControlProtocolGate.NeedsControlProtocol(options, transport));
    }

    [Fact]
    public void NullOptions_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            ControlProtocolGate.NeedsControlProtocol(null!)
        );
    }

    private sealed class NullTransport : ITransport
    {
        public bool IsReady => false;

        public Task ConnectAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WriteAsync(string data, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async IAsyncEnumerable<JsonElement> ReadMessagesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task EndInputAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
