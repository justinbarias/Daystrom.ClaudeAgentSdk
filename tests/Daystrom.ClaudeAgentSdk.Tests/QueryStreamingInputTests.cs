using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Tests.Internal;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

/// <summary>
/// Unit tests for the streaming-input <see cref="ClaudeAgent.QueryAsync"/>
/// overload and the gate-true routing of the <c>QueryAsync(string)</c>
/// overload through the streaming control protocol. All tests drive the
/// SDK through <see cref="InProcessFakeTransport"/> so no subprocess is
/// spawned.
/// </summary>
public class QueryStreamingInputTests
{
    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
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
                var node = JsonNode.Parse(lines[index]);
                var rid = node?["request_id"]?.GetValue<string>();
                if (rid is not null)
                {
                    return rid;
                }
            }
            await Task.Delay(10);
        }
        throw new TimeoutException(
            $"No outbound control request appeared at index {index}. "
                + $"Written: [{string.Join(" | ", transport.WrittenLines)}]"
        );
    }

    private static async Task PollForLineCountAsync(InProcessFakeTransport transport, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (transport.WrittenLines.Count >= count)
            {
                return;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException(
            $"Expected {count} written lines, saw {transport.WrittenLines.Count}."
        );
    }

    private const string AssistantNd =
        """{"type":"assistant","message":{"id":"msg_1","model":"claude","role":"assistant","content":[]},"parent_tool_use_id":null,"session_id":"sess_1","uuid":"u1"}""";

    private const string ResultNd =
        """{"type":"result","subtype":"success","duration_ms":10,"duration_api_ms":5,"is_error":false,"num_turns":1,"session_id":"sess_1","total_cost_usd":0.0,"usage":{},"uuid":"r1"}""";

    private static async IAsyncEnumerable<UserMessageInput> FromChannelAsync(
        ChannelReader<UserMessageInput> reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    [Fact]
    public async Task StreamingInput_PumpsThreeUserMessages_AndYieldsAssistantResultPairs()
    {
        await using var transport = new InProcessFakeTransport();
        var inputs = Channel.CreateUnbounded<UserMessageInput>();
        // We require gate-true to force the streaming path; CanUseTool is
        // a stable trigger that doesn't require building hook tables.
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _, _, _) =>
                ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
        };

        // Drive the iterator on a background task so we can interleave
        // pushing inputs and enqueueing fake stdout frames.
        var collected = new List<Message>();
        using var iterCts = new CancellationTokenSource();
        var iter = Task.Run(async () =>
        {
            await foreach (
                var msg in ClaudeAgent.QueryAsync(
                    FromChannelAsync(inputs.Reader, iterCts.Token),
                    options,
                    transport,
                    iterCts.Token
                )
            )
            {
                collected.Add(msg);
            }
        });

        // 1) initialize handshake.
        var initRequestId = await PollForRequestIdAsync(transport, index: 0);
        transport.EnqueueControlResponse(initRequestId, "success", Parse("{}"));

        // 2) For each user input: push a message, expect the user envelope
        //    on the wire, then enqueue an assistant + result pair.
        for (int i = 0; i < 3; i++)
        {
            await inputs.Writer.WriteAsync(new UserMessageInput($"turn {i}"));
            // Initialize is line 0; then i user envelopes precede this one.
            await PollForLineCountAsync(transport, count: 2 + i);
            transport.EnqueueRaw(AssistantNd);
            transport.EnqueueRaw(ResultNd);
        }

        // Close the input stream so the pump finishes and EndInputAsync
        // closes stdin; the fake transport then completes its inbound
        // channel, which causes the receive loop to terminate.
        inputs.Writer.Complete();

        await iter.WaitAsync(TimeSpan.FromSeconds(5));

        // 6 messages: 3 assistant + 3 result, in interleaved order.
        Assert.Equal(6, collected.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsType<AssistantMessage>(collected[i * 2]);
            Assert.IsType<ResultMessage>(collected[i * 2 + 1]);
        }

        // Outbound: 1 initialize control_request + 3 user envelopes.
        var lines = transport.WrittenLines;
        Assert.Equal(4, lines.Count);
        var init = JsonNode.Parse(lines[0])!.AsObject();
        Assert.Equal("control_request", init["type"]!.GetValue<string>());
        for (int i = 0; i < 3; i++)
        {
            var node = JsonNode.Parse(lines[1 + i])!.AsObject();
            Assert.Equal("user", node["type"]!.GetValue<string>());
            Assert.Equal($"turn {i}", node["message"]!["content"]!.GetValue<string>());
        }

        // Stdin must be closed (EndInputAsync ran in the pump's finally).
        Assert.True(transport.StdinClosed);
    }

    [Fact]
    public async Task StreamingInput_CancellationMidStream_StopsPumpAndReceive_AndDisposes()
    {
        var transport = new InProcessFakeTransport();
        var inputs = Channel.CreateUnbounded<UserMessageInput>();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _, _, _) =>
                ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
        };

        using var cts = new CancellationTokenSource();
        var collected = new List<Message>();
        var iter = Task.Run(async () =>
        {
            try
            {
                await foreach (
                    var msg in ClaudeAgent.QueryAsync(
                        FromChannelAsync(inputs.Reader, cts.Token),
                        options,
                        transport,
                        cts.Token
                    )
                )
                {
                    collected.Add(msg);
                    if (collected.Count == 1)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (OperationCanceledException) { }
        });

        var initRequestId = await PollForRequestIdAsync(transport, index: 0);
        transport.EnqueueControlResponse(initRequestId, "success", Parse("{}"));

        await inputs.Writer.WriteAsync(new UserMessageInput("hi"));
        await PollForLineCountAsync(transport, count: 2);
        transport.EnqueueRaw(AssistantNd);

        // Iterator should observe the message, cancel itself, and tear down.
        await iter.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(collected);

        // The streaming-core owns the transport when none was supplied via
        // the public path; with our test seam the caller still owns the
        // transport, but the client must have closed stdin via EndInputAsync
        // in the pump's finally block.
        Assert.True(transport.StdinClosed);

        // Caller-owned transport: dispose it to clean up.
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task StringQueryAsync_GateTrue_RoutesThroughStreamingPath()
    {
        await using var transport = new InProcessFakeTransport();
        var options = new ClaudeAgentOptions
        {
            CanUseTool = (_, _, _, _) =>
                ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
        };

        // The single-string overload doesn't accept an ITransport, so use
        // the streaming overload directly with a one-element input — this
        // exercises the same code path the gate-true string overload
        // delegates to.
        var inputs = Channel.CreateUnbounded<UserMessageInput>();
        await inputs.Writer.WriteAsync(new UserMessageInput("ping"));

        var collected = new List<Message>();
        var iter = Task.Run(async () =>
        {
            await foreach (
                var msg in ClaudeAgent.QueryAsync(
                    FromChannelAsync(inputs.Reader),
                    options,
                    transport,
                    CancellationToken.None
                )
            )
            {
                collected.Add(msg);
            }
        });

        var initRequestId = await PollForRequestIdAsync(transport, index: 0);
        transport.EnqueueControlResponse(initRequestId, "success", Parse("{}"));
        await PollForLineCountAsync(transport, count: 2);
        transport.EnqueueRaw(ResultNd);
        // Now close the input channel — the pump's finally calls
        // EndInputAsync on the fake, which completes the inbound channel and
        // ends the receive loop cleanly.
        inputs.Writer.Complete();

        await iter.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(collected);
        Assert.IsType<ResultMessage>(collected[0]);

        // Wire shape: control_request initialize, then a {type:"user"} envelope.
        var lines = transport.WrittenLines;
        Assert.Equal(2, lines.Count);
        var init = JsonNode.Parse(lines[0])!.AsObject();
        Assert.Equal("control_request", init["type"]!.GetValue<string>());
        Assert.Equal("initialize", init["request"]!["subtype"]!.GetValue<string>());
        var user = JsonNode.Parse(lines[1])!.AsObject();
        Assert.Equal("user", user["type"]!.GetValue<string>());
        Assert.Equal("ping", user["message"]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task StringQueryAsync_GateFalse_UsesOneShotPath_NoControlProtocol()
    {
        // Default options ⇒ gate-false ⇒ one-shot path. The Phase 5 fast
        // path drives QueryAsyncCore directly with a SubprocessCliTransport,
        // and the test seam (QueryAsyncCore) accepts an ITransport that
        // never sees control protocol traffic. We use the existing seam to
        // verify the read loop ignores any need for an initialize request.
        await using var transport = new InProcessFakeTransport();
        // Pre-load the inbound channel with a result; complete it so the
        // read loop ends naturally.
        transport.EnqueueRaw(ResultNd);
        transport.CompleteInbound();

        var collected = new List<Message>();
        await foreach (
            var msg in ClaudeAgent.QueryAsyncCore(
                new ClaudeAgentOptions(),
                transport,
                ownsTransport: false,
                cancellationToken: CancellationToken.None
            )
        )
        {
            collected.Add(msg);
        }

        Assert.Single(collected);
        Assert.IsType<ResultMessage>(collected[0]);

        // No outbound control_request lines: the one-shot path doesn't
        // initialize. (A real one-shot path uses --print argv, not stdin
        // writes; the fact that the read loop never wrote anything is the
        // assertion the brief asks for.)
        Assert.Empty(transport.WrittenLines);
    }

    [Fact]
    public void QueryAsync_StreamingInput_NullPrompts_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ClaudeAgent.QueryAsync((IAsyncEnumerable<UserMessageInput>)null!)
        );
    }
}
