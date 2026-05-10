using System.Text.Json;
using System.Text.Json.Nodes;
using Daystrom.ClaudeAgentSdk.Control;
using Daystrom.ClaudeAgentSdk.Control.Requests;
using Daystrom.ClaudeAgentSdk.Json;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

/// <summary>
/// Wire-shape round-trip tests for the Phase 6.1 control-protocol envelope
/// and payload records. Each test loads a fixture, deserialises through
/// the source-gen <see cref="ClaudeAgentJsonContext"/>, asserts the
/// discriminator routed to the expected concrete type, then re-serialises
/// and asserts structural JSON equality (whitespace-insensitive) against
/// the fixture.
/// </summary>
public class ControlMessagesTests
{
    private static readonly JsonNodeOptions NodeOptions = new();

    /// <summary>
    /// Walks a <see cref="JsonNode"/> tree and removes object properties
    /// whose value is JSON <c>null</c>. The source-gen context emits with
    /// <c>WhenWritingNull</c>, so a deserialise → reserialise round trip
    /// drops explicit-null optional fields. The fixtures keep the null
    /// fields visible (mirroring what the Python SDK emits at e.g.
    /// <c>query.py:196–199</c>); for structural equality we collapse both
    /// sides to "null is the same as absent".
    /// </summary>
    private static JsonNode? StripNulls(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var result = new JsonObject();
                foreach (var kvp in obj)
                {
                    if (kvp.Value is null)
                        continue;
                    result[kvp.Key] = StripNulls(kvp.Value);
                }
                return result;
            }
            case JsonArray arr:
            {
                var result = new JsonArray();
                foreach (var item in arr)
                    result.Add(StripNulls(item));
                return result;
            }
            default:
                return node?.DeepClone();
        }
    }

    private static void AssertStructurallyEqual(string fixtureJson, string serialized)
    {
        var expected = StripNulls(JsonNode.Parse(fixtureJson, NodeOptions));
        var actual = StripNulls(JsonNode.Parse(serialized, NodeOptions));
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"Structural mismatch.\nExpected:\n{expected?.ToJsonString()}\nActual:\n{actual?.ToJsonString()}"
        );
    }

    [Fact]
    public void InitializeRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/initialize_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        Assert.Equal("control_request", envelope!.Type);
        Assert.Equal("req_1_a8c0bc91", envelope.RequestId);
        var init = Assert.IsType<InitializeRequest>(envelope.Request);
        Assert.True(init.ExcludeDynamicSections);
        Assert.NotNull(init.Skills);
        Assert.Equal(2, init.Skills!.Count);
        Assert.Equal("python", init.Skills[0]);
        Assert.Null(init.Hooks);
        Assert.Null(init.Agents);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void InterruptRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/interrupt_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        Assert.IsType<InterruptRequest>(envelope!.Request);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void SetPermissionModeRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/set_permission_mode_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        var set = Assert.IsType<SetPermissionModeRequest>(envelope!.Request);
        Assert.Equal(PermissionMode.Plan, set.Mode);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void McpStatusRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/mcp_status_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        Assert.IsType<McpStatusRequest>(envelope!.Request);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void GetContextUsageRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/get_context_usage_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        Assert.IsType<GetContextUsageRequest>(envelope!.Request);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void CanUseToolRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/can_use_tool_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        var ask = Assert.IsType<CanUseToolRequest>(envelope!.Request);
        Assert.Equal("Bash", ask.ToolName);
        Assert.Equal("toolu_01ABCDEF", ask.ToolUseId);
        Assert.Null(ask.AgentId);
        Assert.Equal(JsonValueKind.Object, ask.Input.ValueKind);
        Assert.Equal("ls -la /tmp", ask.Input.GetProperty("command").GetString());
        Assert.NotNull(ask.PermissionSuggestions);
        Assert.Single(ask.PermissionSuggestions!);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void HookCallbackRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/hook_callback_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        var hook = Assert.IsType<HookCallbackRequest>(envelope!.Request);
        Assert.Equal("hook_3", hook.CallbackId);
        Assert.Equal("toolu_01ABCDEF", hook.ToolUseId);
        Assert.Equal(JsonValueKind.Object, hook.Input.ValueKind);
        Assert.Equal("PreToolUse", hook.Input.GetProperty("hook_event_name").GetString());

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void McpMessageRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/mcp_message_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        Assert.NotNull(envelope);
        var mcp = Assert.IsType<McpMessageRequest>(envelope!.Request);
        Assert.Equal("my-tools", mcp.ServerName);
        Assert.Equal(JsonValueKind.Object, mcp.Message.ValueKind);
        Assert.Equal("tools/call", mcp.Message.GetProperty("method").GetString());

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void ControlResponse_Success_RoundTrips()
    {
        var json = Fixtures.Read("control/control_response_success.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlResponseEnvelope
        );
        Assert.NotNull(envelope);
        Assert.Equal("control_response", envelope!.Type);
        Assert.Equal("success", envelope.Response.Subtype);
        Assert.Equal("req_4_2bf099a1", envelope.Response.RequestId);
        Assert.Null(envelope.Response.Error);
        Assert.NotNull(envelope.Response.Response);
        Assert.Equal(JsonValueKind.Object, envelope.Response.Response!.Value.ValueKind);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlResponseEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void ControlResponse_Error_RoundTrips()
    {
        var json = Fixtures.Read("control/control_response_error.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlResponseEnvelope
        );
        Assert.NotNull(envelope);
        Assert.Equal("error", envelope!.Response.Subtype);
        Assert.Equal("Control request timeout: interrupt", envelope.Response.Error);
        Assert.Null(envelope.Response.Response);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlResponseEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }

    [Fact]
    public void ControlCancelRequest_RoundTrips()
    {
        var json = Fixtures.Read("control/control_cancel_request.json");
        var envelope = JsonSerializer.Deserialize(
            json,
            ClaudeAgentJsonContext.Default.ControlCancelRequestEnvelope
        );
        Assert.NotNull(envelope);
        Assert.Equal("control_cancel_request", envelope!.Type);
        Assert.Equal("cli_req_42_d3aa97ef", envelope.RequestId);

        var roundtrip = JsonSerializer.Serialize(
            envelope,
            ClaudeAgentJsonContext.Default.ControlCancelRequestEnvelope
        );
        AssertStructurallyEqual(json, roundtrip);
    }
}
