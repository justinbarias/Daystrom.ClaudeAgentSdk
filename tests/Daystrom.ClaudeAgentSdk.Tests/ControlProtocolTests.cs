using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Control;
using Daystrom.ClaudeAgentSdk.Control.Requests;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Tests.Internal;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

/// <summary>
/// Unit tests for <see cref="ControlProtocol"/>. Covers the round-trip
/// correlation table, error surfacing, write-lock serialization, inbound
/// dispatch (including the cancel-in-flight semantic from Phase 6 plan
/// decision 1), disposal cleanup, and SDK-message forwarding.
/// </summary>
public class ControlProtocolTests
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

    [Fact]
    public async Task Initialize_RoundTrip_ResolvesAndWritesExpectedShape()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var send = protocol.SendRequestAsync<InitializeRequest>(new InitializeRequest());

        // Wait for the protocol to write the request, then echo a success.
        var requestId = await PollForRequestIdAsync(transport);
        transport.EnqueueControlResponse(requestId, "success", Parse("{}"));

        await send.WaitAsync(TimeSpan.FromSeconds(2));

        var written = transport.WrittenLines;
        Assert.Single(written);
        var line = written[0];
        var doc = JsonNode.Parse(line)!.AsObject();
        Assert.Equal("control_request", doc["type"]!.GetValue<string>());
        Assert.Equal(requestId, doc["request_id"]!.GetValue<string>());
        Assert.Equal("initialize", doc["request"]!["subtype"]!.GetValue<string>());
    }

    [Fact]
    public async Task ErrorResponse_SurfacesAsClaudeSdkException()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var send = protocol.SendRequestAsync<InterruptRequest>(new InterruptRequest());
        var requestId = await PollForRequestIdAsync(transport);
        transport.EnqueueControlResponse(requestId, "error", error: "interrupt blew up");

        var ex = await Assert.ThrowsAsync<ClaudeSdkException>(() =>
            send.WaitAsync(TimeSpan.FromSeconds(2))
        );
        Assert.Contains("interrupt blew up", ex.Message);
    }

    [Fact]
    public async Task ConcurrentSendRequest_DoesNotInterleaveWrittenLines()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        const int N = 50;
        var sends = new List<Task>();
        for (var i = 0; i < N; i++)
        {
            sends.Add(protocol.SendRequestAsync<InterruptRequest>(new InterruptRequest()));
        }

        // Echo a success per request_id as soon as each one is observed.
        var seen = new HashSet<string>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (seen.Count < N && DateTime.UtcNow < deadline)
        {
            foreach (var line in transport.WrittenLines)
            {
                var rid = GetRequestId(line);
                if (seen.Add(rid))
                {
                    transport.EnqueueControlResponse(rid, "success", Parse("{}"));
                }
            }
            await Task.Delay(10);
        }

        await Task.WhenAll(sends).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(N, seen.Count);

        var written = transport.WrittenLines;
        Assert.Equal(N, written.Count);
        // Each line is independently parseable JSON => no interleave.
        var ids = new HashSet<string>();
        foreach (var line in written)
        {
            var node = JsonNode.Parse(line);
            Assert.NotNull(node);
            Assert.True(ids.Add(node!["request_id"]!.GetValue<string>()));
        }
        Assert.Equal(N, ids.Count);
    }

    [Fact]
    public async Task InboundControlRequest_WithoutHandler_WritesUnsupportedSubtypeError()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        // Kick the read loop.
        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        var extras = Parse(
            """
            {
              "tool_name": "Bash",
              "input": { "command": "ls" }
            }
            """
        );
        transport.EnqueueControlRequest("cli_req_1", "can_use_tool", extras);

        var line = await PollForWrittenAsync(transport, count: 1);
        var node = JsonNode.Parse(line[0])!.AsObject();
        Assert.Equal("control_response", node["type"]!.GetValue<string>());
        var response = node["response"]!.AsObject();
        Assert.Equal("error", response["subtype"]!.GetValue<string>());
        Assert.Equal("cli_req_1", response["request_id"]!.GetValue<string>());
        Assert.Contains("can_use_tool", response["error"]!.GetValue<string>());

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task InboundControlRequest_WithHandler_WritesSuccessResponseWithBody()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        using var registration = protocol.RegisterInboundHandler<CanUseToolRequest>(
            (req, ct) => Task.FromResult(Parse("{\"behavior\":\"allow\",\"updatedInput\":{}}"))
        );

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        var extras = Parse(
            """
            {
              "tool_name": "Bash",
              "input": { "command": "ls" }
            }
            """
        );
        transport.EnqueueControlRequest("cli_req_2", "can_use_tool", extras);

        var lines = await PollForWrittenAsync(transport, count: 1);
        var node = JsonNode.Parse(lines[0])!.AsObject();
        var response = node["response"]!.AsObject();
        Assert.Equal("success", response["subtype"]!.GetValue<string>());
        Assert.Equal("cli_req_2", response["request_id"]!.GetValue<string>());
        Assert.Equal("allow", response["response"]!["behavior"]!.GetValue<string>());

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task InboundControlCancel_CancelsInFlightHandler_AndDropsResponse()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var handlerStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var cancelObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        using var registration = protocol.RegisterInboundHandler<CanUseToolRequest>(
            async (req, ct) =>
            {
                handlerStarted.TrySetResult();
                using var reg = ct.Register(() => cancelObserved.TrySetResult());
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    // Bubble out so the protocol's "cancelled => drop" path runs.
                    throw;
                }
                return Parse("{}");
            }
        );

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        var extras = Parse(
            """
            {
              "tool_name": "Bash",
              "input": { "command": "ls" }
            }
            """
        );
        transport.EnqueueControlRequest("cli_req_3", "can_use_tool", extras);

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);
        transport.EnqueueControlCancel("cli_req_3");

        await cancelObserved.Task.WaitAsync(TimeSpan.FromMilliseconds(500));

        // Allow the handler to finish unwinding and any (hypothetical)
        // response to be written. None should be.
        await Task.Delay(100);
        Assert.Empty(transport.WrittenLines);

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }
    }

    [Theory]
    [InlineData(null, 60_000)]
    [InlineData("120000", 120_000)]
    [InlineData("30000", 60_000)]
    [InlineData("garbage", 60_000)]
    public void ResolveInitializeTimeout_ParsesEnvWithFloor(string? value, int expectedMs)
    {
        var key = "CLAUDE_CODE_STREAM_CLOSE_TIMEOUT";
        var prior = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, value);
            var ts = ControlProtocol.ResolveInitializeTimeout();
            Assert.Equal(expectedMs, (int)ts.TotalMilliseconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, prior);
        }
    }

    [Fact]
    public async Task Dispose_FaultsPendingRequest()
    {
        var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        var protocol = new ControlProtocol(transport);

        var send = protocol.SendRequestAsync<InterruptRequest>(new InterruptRequest());
        // Wait until the request was written so we know the read loop is up.
        await PollForRequestIdAsync(transport);

        await protocol.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            send.WaitAsync(TimeSpan.FromSeconds(2))
        );

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task ReadLoop_ForwardsNonControlMessagesToSdkChannel()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        const string AssistantLine = """
            {"type":"assistant","message":{"id":"msg_1","model":"claude","role":"assistant","content":[]},"parent_tool_use_id":null,"session_id":"sess_1","uuid":"u1"}
            """;
        transport.EnqueueRaw(AssistantLine);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var element in protocol.ReadSdkMessagesAsync(cts.Token))
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
            Assert.Equal("assistant", element.GetProperty("type").GetString());
            return; // first message is enough.
        }
        Assert.Fail("No SDK messages surfaced.");
    }

    [Fact]
    public async Task RegisterInboundHandler_DuplicateForSameType_Throws()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        using var reg = protocol.RegisterInboundHandler<CanUseToolRequest>(
            (_, _) => Task.FromResult(Parse("{}"))
        );

        Assert.Throws<InvalidOperationException>(() =>
            protocol.RegisterInboundHandler<CanUseToolRequest>(
                (_, _) => Task.FromResult(Parse("{}"))
            )
        );
    }

    [Fact]
    public async Task RegisterInboundHandler_DisposingRegistration_AllowsReRegistration()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var first = protocol.RegisterInboundHandler<CanUseToolRequest>(
            (_, _) => Task.FromResult(Parse("{}"))
        );
        first.Dispose();

        using var second = protocol.RegisterInboundHandler<CanUseToolRequest>(
            (_, _) => Task.FromResult(Parse("{}"))
        );
        Assert.NotNull(second);
    }

    [Fact]
    public async Task ControlResponse_ForUnknownRequestId_IsIgnored()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        transport.EnqueueControlResponse("orphan_id", "success", Parse("{}"));

        await Task.Delay(100);

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task ControlResponse_MalformedFrame_IsIgnoredByReadLoop()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        transport.EnqueueRaw("""{"type":"control_response"}""");
        await Task.Delay(100);

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task ControlRequest_MalformedFrame_IsIgnoredByReadLoop()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        transport.EnqueueRaw("""{"type":"control_request","request_id":"r1"}""");
        await Task.Delay(100);

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }

        Assert.Empty(transport.WrittenLines);
    }

    [Fact]
    public async Task ControlRequest_DuplicateInFlightRequestId_IsDropped()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = protocol.RegisterInboundHandler<CanUseToolRequest>(
            async (req, ct) =>
            {
                gate.TrySetResult();
                await release.Task.WaitAsync(ct);
                return Parse("{}");
            }
        );

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        var extras = Parse("""{"tool_name":"Bash","input":{"command":"ls"}}""");
        transport.EnqueueControlRequest("dup_req", "can_use_tool", extras);
        await gate.Task.WaitAsync(TimeSpan.FromSeconds(2));

        transport.EnqueueControlRequest("dup_req", "can_use_tool", extras);
        await Task.Delay(100);

        release.TrySetResult();
        await PollForWrittenAsync(transport, 1);

        Assert.Single(transport.WrittenLines);

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task ControlCancel_MalformedFrame_IsIgnoredByReadLoop()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        transport.EnqueueRaw("""{"type":"control_cancel_request"}""");
        await Task.Delay(100);

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }

        Assert.Empty(transport.WrittenLines);
    }

    [Fact]
    public async Task ControlCancel_ForUnknownRequestId_IsIgnored()
    {
        await using var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        await using var protocol = new ControlProtocol(transport);

        var iterator = StartConsumingSdkMessages(protocol, out var cts);

        transport.EnqueueControlCancel("no_such_request");
        await Task.Delay(100);

        cts.Cancel();
        try
        {
            await iterator;
        }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task SendRequest_AfterDispose_Throws()
    {
        var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        var protocol = new ControlProtocol(transport);
        await protocol.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await protocol.SendRequestAsync<InterruptRequest>(new InterruptRequest());
        });

        await transport.DisposeAsync();
    }

    [Fact]
    public async Task WriteRaw_AfterDispose_Throws()
    {
        var transport = new InProcessFakeTransport();
        await transport.ConnectAsync();
        var protocol = new ControlProtocol(transport);
        await protocol.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => protocol.WriteRawAsync("noop"));

        await transport.DisposeAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static async Task<string> PollForRequestIdAsync(InProcessFakeTransport transport)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var lines = transport.WrittenLines;
            if (lines.Count > 0)
            {
                return GetRequestId(lines[^1]);
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

    private static Task StartConsumingSdkMessages(
        ControlProtocol protocol,
        out CancellationTokenSource cts
    )
    {
        var localCts = new CancellationTokenSource();
        cts = localCts;
        return Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in protocol.ReadSdkMessagesAsync(localCts.Token))
                {
                    // Drain.
                }
            }
            catch (OperationCanceledException) { }
        });
    }
}
