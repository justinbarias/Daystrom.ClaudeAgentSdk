using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Tests.Internal;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

/// <summary>
/// Unit tests for <see cref="ClaudeAgentClient"/>. All tests drive the
/// client through <see cref="InProcessFakeTransport"/> so no subprocess is
/// spawned. Mirrors patterns from <see cref="ControlProtocolTests"/>.
/// </summary>
public class ClaudeAgentClientTests
{
    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string GetRequestId(string ndjsonLine)
    {
        var node = JsonNode.Parse(ndjsonLine);
        return node!["request_id"]!.GetValue<string>();
    }

    private static async Task<string> PollForRequestIdAsync(
        InProcessFakeTransport transport,
        int index = 0
    )
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var lines = transport.WrittenLines;
            if (lines.Count > index)
            {
                return GetRequestId(lines[index]);
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("No outbound control request appeared.");
    }

    private static async Task<IReadOnlyList<string>> PollForWrittenAsync(
        InProcessFakeTransport transport,
        int count
    )
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var lines = transport.WrittenLines;
            if (lines.Count >= count)
            {
                return lines;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException(
            $"Expected {count} outbound line(s) but saw {transport.WrittenLines.Count}."
        );
    }

    private static async Task<IClaudeAgentClient> ConnectFakeClientAsync(
        InProcessFakeTransport transport,
        ClaudeAgentOptions? options = null
    )
    {
        var client = ClaudeAgentClient.Create(options ?? new ClaudeAgentOptions(), transport);
        var connect = client.ConnectAsync();
        var requestId = await PollForRequestIdAsync(transport);
        transport.EnqueueControlResponse(requestId, "success", Parse("{}"));
        await connect.WaitAsync(TimeSpan.FromSeconds(2));
        return client;
    }

    [Fact]
    public async Task ConnectAsync_RunsInitializeHandshake()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = ClaudeAgentClient.Create(new ClaudeAgentOptions(), transport);

        var connectTask = client.ConnectAsync();
        var requestId = await PollForRequestIdAsync(transport);
        transport.EnqueueControlResponse(requestId, "success", Parse("{}"));
        await connectTask.WaitAsync(TimeSpan.FromSeconds(2));

        var written = transport.WrittenLines;
        Assert.Single(written);
        var node = JsonNode.Parse(written[0])!.AsObject();
        Assert.Equal("control_request", node["type"]!.GetValue<string>());
        Assert.Equal("initialize", node["request"]!["subtype"]!.GetValue<string>());
    }

    [Fact]
    public async Task ConnectAsync_IsIdempotent()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        // Second call should not produce an additional outbound line.
        await client.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(transport.WrittenLines);
    }

    [Fact]
    public async Task ConnectAsync_InitializeFailure_DisposesOwnedTransport()
    {
        var transport = new InProcessFakeTransport();
        var client = ClaudeAgentClient.CreateForTest(
            new ClaudeAgentOptions(),
            transport,
            ownsTransport: true
        );

        var connectTask = client.ConnectAsync();
        var requestId = await PollForRequestIdAsync(transport);
        transport.EnqueueControlResponse(requestId, "error", error: "init blew up");

        var ex = await Assert.ThrowsAsync<ClaudeSdkException>(() =>
            connectTask.WaitAsync(TimeSpan.FromSeconds(2))
        );
        Assert.Contains("init blew up", ex.Message);

        // Owned transport must have been disposed by the failure path.
        Assert.True(transport.Disposed);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task SendUserMessageAsync_WritesExpectedWireEnvelope()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        await client.SendUserMessageAsync("hello").WaitAsync(TimeSpan.FromSeconds(2));

        var lines = transport.WrittenLines;
        Assert.Equal(2, lines.Count);
        var line = lines[1];
        Assert.EndsWith("\n", line);
        var node = JsonNode.Parse(line)!.AsObject();
        Assert.Equal("user", node["type"]!.GetValue<string>());
        Assert.Equal("", node["session_id"]!.GetValue<string>());
        var message = node["message"]!.AsObject();
        Assert.Equal("user", message["role"]!.GetValue<string>());
        Assert.Equal("hello", message["content"]!.GetValue<string>());
        Assert.Null(node["parent_tool_use_id"]?.GetValue<string?>());
    }

    [Fact]
    public async Task SendUserMessageAsync_WithStructuredInput_WritesExpectedFields()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        var input = new UserMessageInput("ping")
        {
            SessionId = "sess-42",
            ParentToolUseId = "tu_7",
        };
        await client.SendUserMessageAsync(input).WaitAsync(TimeSpan.FromSeconds(2));

        var lines = transport.WrittenLines;
        var node = JsonNode.Parse(lines[1])!.AsObject();
        Assert.Equal("sess-42", node["session_id"]!.GetValue<string>());
        Assert.Equal("tu_7", node["parent_tool_use_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task SendUserMessageAsync_AfterEndInput_ThrowsInvalidOperation()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        await client.EndInputAsync().WaitAsync(TimeSpan.FromSeconds(2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendUserMessageAsync("post-end")
        );
        Assert.Contains("agent session has ended", ex.Message);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_ParsesEnqueuedMessages()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        const string Assistant = """
            {"type":"assistant","message":{"id":"msg_1","model":"claude","role":"assistant","content":[]},"parent_tool_use_id":null,"session_id":"sess_1","uuid":"u1"}
            """;
        const string Result = """
            {"type":"result","subtype":"success","duration_ms":10,"duration_api_ms":5,"is_error":false,"num_turns":1,"session_id":"sess_1","total_cost_usd":0.0,"usage":{},"uuid":"r1"}
            """;
        transport.EnqueueRaw(Assistant);
        transport.EnqueueRaw(Result);

        var collected = new List<Message>();
        using var cts = new CancellationTokenSource();
        var iteration = Task.Run(async () =>
        {
            await foreach (var m in client.ReceiveMessagesAsync(cts.Token))
            {
                collected.Add(m);
                if (collected.Count == 2)
                {
                    cts.Cancel();
                }
            }
        });

        try
        {
            await iteration.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException) { }

        Assert.Equal(2, collected.Count);
        Assert.IsType<AssistantMessage>(collected[0]);
        Assert.IsType<ResultMessage>(collected[1]);
    }

    [Fact]
    public async Task InterruptAsync_WritesInterruptAndAwaitsAck()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        var interrupt = client.InterruptAsync();
        var requestId = await PollForRequestIdAsync(transport, index: 1);
        transport.EnqueueControlResponse(requestId, "success", Parse("{}"));
        await interrupt.WaitAsync(TimeSpan.FromSeconds(2));

        var line = transport.WrittenLines[1];
        var node = JsonNode.Parse(line)!.AsObject();
        Assert.Equal("interrupt", node["request"]!["subtype"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetMcpStatusAsync_DeserialisesTypedResponse()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        var body = Parse(
            """
            { "mcpServers": [ { "name": "weather", "status": "connected" } ] }
            """
        );

        var task = client.GetMcpStatusAsync();
        var requestId = await PollForRequestIdAsync(transport, index: 1);
        transport.EnqueueControlResponse(requestId, "success", body);
        var response = await task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotNull(response);
        Assert.Single(response.McpServers);
        Assert.Equal("weather", response.McpServers[0].Name);
    }

    [Fact]
    public async Task GetContextUsageAsync_DeserialisesTypedResponse()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        var body = Parse(
            """
            {
              "categories": [],
              "totalTokens": 1234,
              "maxTokens": 200000,
              "rawMaxTokens": 200000,
              "percentage": 0.6,
              "model": "claude-sonnet-4-5",
              "isAutoCompactEnabled": true,
              "memoryFiles": [],
              "mcpTools": [],
              "agents": [],
              "gridRows": []
            }
            """
        );

        var task = client.GetContextUsageAsync();
        var requestId = await PollForRequestIdAsync(transport, index: 1);
        transport.EnqueueControlResponse(requestId, "success", body);
        var response = await task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1234, response.TotalTokens);
        Assert.Equal("claude-sonnet-4-5", response.Model);
        Assert.True(response.IsAutoCompactEnabled);
    }

    [Fact]
    public async Task ApplyPermissionUpdateAsync_SetMode_WritesExpectedSubtypeAndMode()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        var update = new PermissionUpdate { Type = "setMode", Mode = PermissionMode.AcceptEdits };
        var task = client.ApplyPermissionUpdateAsync(update);
        var requestId = await PollForRequestIdAsync(transport, index: 1);
        transport.EnqueueControlResponse(requestId, "success", Parse("{}"));
        await task.WaitAsync(TimeSpan.FromSeconds(2));

        var line = transport.WrittenLines[1];
        var node = JsonNode.Parse(line)!.AsObject();
        var request = node["request"]!.AsObject();
        Assert.Equal("set_permission_mode", request["subtype"]!.GetValue<string>());
        Assert.Equal("acceptEdits", request["mode"]!.GetValue<string>());
    }

    [Fact]
    public async Task ApplyPermissionUpdateAsync_RuleUpdate_ThrowsNotSupported()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = await ConnectFakeClientAsync(transport);

        var update = new PermissionUpdate
        {
            Type = "addRules",
            Rules = Array.Empty<PermissionRuleValue>(),
            Behavior = PermissionBehavior.Allow,
        };

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            client.ApplyPermissionUpdateAsync(update)
        );
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var transport = new InProcessFakeTransport();
        var client = await ConnectFakeClientAsync(transport);

        await client.DisposeAsync();
        await client.DisposeAsync();

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CancelsInFlightReceiveMessages()
    {
        var transport = new InProcessFakeTransport();
        var client = await ConnectFakeClientAsync(transport);

        var iteration = Task.Run(async () =>
        {
            await foreach (var _ in client.ReceiveMessagesAsync())
            {
                // Drain.
            }
        });

        // Give the iterator a moment to enter the wait.
        await Task.Delay(50);

        await client.DisposeAsync();

        // Iterator must complete (either cleanly via channel-completion or
        // with cancellation) within a short budget.
        try
        {
            await iteration.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException) { }

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task SendUserMessageAsync_BeforeConnect_Throws()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = ClaudeAgentClient.Create(new ClaudeAgentOptions(), transport);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendUserMessageAsync("nope")
        );
        Assert.Contains("not connected", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_BeforeConnect_Throws()
    {
        await using var transport = new InProcessFakeTransport();
        await using var client = ClaudeAgentClient.Create(new ClaudeAgentOptions(), transport);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.ReceiveMessagesAsync())
            {
                break;
            }
        });
    }

    [Fact]
    public async Task ConnectAsync_WithSystemPromptPreset_PassesExcludeDynamicSections()
    {
        await using var transport = new InProcessFakeTransport();
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new SystemPrompt.SystemPromptPreset(
                Append: null,
                ExcludeDynamicSections: true
            ),
        };
        await using var client = ClaudeAgentClient.Create(options, transport);

        var connectTask = client.ConnectAsync();
        var requestId = await PollForRequestIdAsync(transport);
        transport.EnqueueControlResponse(requestId, "success", Parse("{}"));
        await connectTask.WaitAsync(TimeSpan.FromSeconds(2));

        var node = JsonNode.Parse(transport.WrittenLines[0])!.AsObject();
        var request = node["request"]!.AsObject();
        Assert.Equal("initialize", request["subtype"]!.GetValue<string>());
        Assert.True(request["excludeDynamicSections"]!.GetValue<bool>());
    }
}
