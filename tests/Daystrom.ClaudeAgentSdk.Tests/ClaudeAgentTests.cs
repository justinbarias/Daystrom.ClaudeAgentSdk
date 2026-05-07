using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Hooks;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Transport;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

public class ClaudeAgentTests
{
    [Fact]
    public async Task QueryAsyncCore_YieldsParsedMessages_InOrder()
    {
        var transport = new ScriptedTransport();
        transport.Push(Fixtures.Read("message/assistant.json"));
        transport.Push(Fixtures.Read("message/result_success.json"));
        transport.Complete();

        var collected = await CollectAsync(
            ClaudeAgent.QueryAsyncCore(
                new ClaudeAgentOptions(),
                transport,
                ownsTransport: true,
                cancellationToken: default
            )
        );

        Assert.Collection(
            collected,
            m => Assert.IsType<AssistantMessage>(m),
            m => Assert.IsType<ResultMessage>(m)
        );
        Assert.True(transport.IsConnected);
        Assert.True(transport.IsDisposed);
    }

    [Fact]
    public async Task QueryAsyncCore_SkipsUnknownMessageTypes()
    {
        var transport = new ScriptedTransport();
        transport.Push("""{ "type": "future_unknown_type", "x": 1 }""");
        transport.Push(Fixtures.Read("message/result_success.json"));
        transport.Complete();

        var collected = await CollectAsync(
            ClaudeAgent.QueryAsyncCore(
                new ClaudeAgentOptions(),
                transport,
                ownsTransport: true,
                default
            )
        );

        Assert.Single(collected);
        Assert.IsType<ResultMessage>(collected[0]);
    }

    [Fact]
    public async Task QueryAsyncCore_DisposesOwnedTransport_OnCompletion()
    {
        var transport = new ScriptedTransport();
        transport.Push(Fixtures.Read("message/result_success.json"));
        transport.Complete();

        await CollectAsync(
            ClaudeAgent.QueryAsyncCore(
                new ClaudeAgentOptions(),
                transport,
                ownsTransport: true,
                default
            )
        );

        Assert.True(transport.IsDisposed);
    }

    [Fact]
    public async Task QueryAsyncCore_DoesNotDisposeUnownedTransport()
    {
        var transport = new ScriptedTransport();
        transport.Push(Fixtures.Read("message/result_success.json"));
        transport.Complete();

        await CollectAsync(
            ClaudeAgent.QueryAsyncCore(
                new ClaudeAgentOptions(),
                transport,
                ownsTransport: false,
                default
            )
        );

        Assert.False(transport.IsDisposed);
    }

    [Fact]
    public async Task QueryAsyncCore_CancellationDisposesOwnedTransport()
    {
        var transport = new ScriptedTransport();
        transport.Push(Fixtures.Read("message/assistant.json"));
        // Note: transport is intentionally not Completed — the read loop
        // would otherwise block forever waiting for the next line.

        using var cts = new CancellationTokenSource();
        var enumerator = ClaudeAgent
            .QueryAsyncCore(
                new ClaudeAgentOptions(),
                transport,
                ownsTransport: true,
                cancellationToken: cts.Token
            )
            .GetAsyncEnumerator(cts.Token);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<AssistantMessage>(enumerator.Current);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await enumerator.MoveNextAsync();
        });
        await enumerator.DisposeAsync();

        Assert.True(transport.IsDisposed);
    }

    [Fact]
    public void QueryAsync_ThrowsArgumentNull_ForNullPrompt()
    {
        Assert.Throws<ArgumentNullException>(() => ClaudeAgent.QueryAsync(null!));
    }

    [Fact]
    public void QueryAsync_ThrowsNotSupported_WhenCanUseToolSet()
    {
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (n, i, c, t) =>
                ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
        };
        var ex = Assert.Throws<NotSupportedException>(() =>
            ClaudeAgent.QueryAsync("ping", options)
        );
        Assert.Contains("Phase 6", ex.Message);
    }

    [Fact]
    public void QueryAsync_ThrowsNotSupported_WhenHooksRegistered()
    {
        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
            {
                [HookEvent.PreToolUse] = new[] { new HookMatcher() },
            },
        };
        Assert.Throws<NotSupportedException>(() => ClaudeAgent.QueryAsync("ping", options));
    }

    [Fact]
    public void QueryAsync_ThrowsNotSupported_WhenSdkMcpServerPresent()
    {
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["calc"] = new McpSdkServerConfig { Name = "calc", Instance = null! },
            },
        };
        Assert.Throws<NotSupportedException>(() => ClaudeAgent.QueryAsync("ping", options));
    }

    private static async Task<List<Message>> CollectAsync(IAsyncEnumerable<Message> source)
    {
        var list = new List<Message>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    private sealed class ScriptedTransport : ITransport
    {
        private readonly Channel<JsonElement> _channel = Channel.CreateUnbounded<JsonElement>();
        private readonly List<JsonDocument> _docs = new();

        public bool IsReady => IsConnected && !IsDisposed;
        public bool IsConnected { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Push(string json)
        {
            var doc = JsonDocument.Parse(json);
            _docs.Add(doc);
            _channel.Writer.TryWrite(doc.RootElement);
        }

        public void Complete() => _channel.Writer.Complete();

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(string data, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async IAsyncEnumerable<JsonElement> ReadMessagesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            await foreach (var element in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return element;
            }
        }

        public Task EndInputAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            _channel.Writer.TryComplete();
            foreach (var doc in _docs)
            {
                doc.Dispose();
            }
            return ValueTask.CompletedTask;
        }
    }
}
