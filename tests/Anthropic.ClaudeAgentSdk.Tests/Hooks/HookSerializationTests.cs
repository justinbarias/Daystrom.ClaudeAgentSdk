using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.ClaudeAgentSdk.Hooks;
using Anthropic.ClaudeAgentSdk.Hooks.Inputs;
using Anthropic.ClaudeAgentSdk.Hooks.Outputs;
using Xunit;

namespace Anthropic.ClaudeAgentSdk.Tests.Hooks;

public class HookSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    private const string PreToolUseInput = """
        {
          "hook_event_name": "PreToolUse",
          "session_id": "session-1",
          "transcript_path": "/tmp/t.jsonl",
          "cwd": "/work",
          "tool_name": "Bash",
          "tool_input": { "command": "ls" },
          "tool_use_id": "tool-1"
        }
        """;

    [Fact]
    public void PreToolUseHookInput_DispatchesViaHookEventName()
    {
        var input = JsonSerializer.Deserialize<HookInput>(PreToolUseInput, Options);
        var pre = Assert.IsType<PreToolUseHookInput>(input);
        Assert.Equal("Bash", pre.ToolName);
        Assert.Equal("tool-1", pre.ToolUseId);
        Assert.Equal("session-1", pre.SessionId);
    }

    [Fact]
    public void PreToolUseHookInput_DiscriminatorRoundTrip()
    {
        var input = JsonSerializer.Deserialize<HookInput>(PreToolUseInput, Options);
        var json = JsonSerializer.Serialize(input, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("PreToolUse", doc.RootElement.GetProperty("hook_event_name").GetString());
    }

    [Theory]
    [InlineData(typeof(PostToolUseHookInput), "PostToolUse")]
    [InlineData(typeof(PostToolUseFailureHookInput), "PostToolUseFailure")]
    [InlineData(typeof(UserPromptSubmitHookInput), "UserPromptSubmit")]
    [InlineData(typeof(StopHookInput), "Stop")]
    [InlineData(typeof(SubagentStopHookInput), "SubagentStop")]
    [InlineData(typeof(SubagentStartHookInput), "SubagentStart")]
    [InlineData(typeof(PreCompactHookInput), "PreCompact")]
    [InlineData(typeof(NotificationHookInput), "Notification")]
    [InlineData(typeof(PermissionRequestHookInput), "PermissionRequest")]
    public void HookInputDispatch_AcceptsEachVariant(System.Type runtimeType, string discriminator)
    {
        // Build a minimal valid JSON shape per variant and assert STJ
        // dispatches to the right runtime type. The variants share the
        // BaseHookInput fields plus per-variant required fields; build
        // the latter at runtime per type.
        var json = BuildMinimalInputJson(discriminator);
        var input = JsonSerializer.Deserialize<HookInput>(json, Options);
        Assert.NotNull(input);
        Assert.IsType(runtimeType, input);
    }

    [Fact]
    public void NotificationHookSpecificOutput_DiscriminatorRoundTrip()
    {
        var output = new NotificationHookSpecificOutput { AdditionalContext = "fyi" };
        var json = JsonSerializer.Serialize<HookSpecificOutput>(output, Options);
        using var doc = JsonDocument.Parse(json);
        // Note: hookEventName is camelCase on outputs, NOT snake_case as
        // on the input side — verifies the asymmetry called out in
        // discriminators.md.
        Assert.Equal("Notification", doc.RootElement.GetProperty("hookEventName").GetString());
    }

    [Fact]
    public void HookSpecificOutputDispatch_AcceptsEachVariant()
    {
        foreach (
            var (json, expected) in new (string, System.Type)[]
            {
                (
                    """{"hookEventName":"Notification","additionalContext":"a"}""",
                    typeof(NotificationHookSpecificOutput)
                ),
                (
                    """{"hookEventName":"SubagentStart","additionalContext":"b"}""",
                    typeof(SubagentStartHookSpecificOutput)
                ),
                (
                    """{"hookEventName":"PermissionRequest","decision":{"allow":true}}""",
                    typeof(PermissionRequestHookSpecificOutput)
                ),
                (
                    """{"hookEventName":"PostToolUseFailure","additionalContext":"c"}""",
                    typeof(PostToolUseFailureHookSpecificOutput)
                ),
            }
        )
        {
            var output = JsonSerializer.Deserialize<HookSpecificOutput>(json, Options);
            Assert.NotNull(output);
            Assert.IsType(expected, output);
        }
    }

    [Fact]
    public void HookJsonOutput_SerializesAsyncAndContinueAsKeywordFields()
    {
        var output = new HookJsonOutput { Async = true, Continue = false };
        var json = JsonSerializer.Serialize(output, Options);
        Assert.Contains("\"async\":true", json);
        Assert.Contains("\"continue\":false", json);
    }

    [Fact]
    public async Task ErasedHookHandler_WrapsStronglyTyped()
    {
        // Acceptance: a HookHandler<TInput> closes over to an erased
        // HookHandler delegate without a cast.
        HookHandler<PreToolUseHookInput> typed = (input, toolUseId, ctx, ct) =>
        {
            Assert.Equal("Bash", input.ToolName);
            return ValueTask.FromResult(new HookJsonOutput { Continue = true });
        };

        HookHandler erased = (input, toolUseId, ctx, ct) =>
            typed((PreToolUseHookInput)input, toolUseId, ctx, ct);

        var preInput = JsonSerializer.Deserialize<HookInput>(PreToolUseInput, Options)!;
        var result = await erased(preInput, "tool-1", new HookContext(), CancellationToken.None);
        Assert.True(result.Continue);
    }

    [Fact]
    public void HookResult_VariantsArePatternMatchable()
    {
        HookResult result = new HookResult.Block("not allowed");
        var description = result switch
        {
            HookResult.Continue => "continue",
            HookResult.Block { Reason: var r } => $"block: {r}",
            HookResult.Modify => "modify",
            _ => "unknown",
        };
        Assert.Equal("block: not allowed", description);
    }

    private static string BuildMinimalInputJson(string discriminator)
    {
        const string baseFields = """
            "session_id": "s",
            "transcript_path": "/t",
            "cwd": "/c"
            """;

        return discriminator switch
        {
            "PostToolUse" => $$"""
                {
                  "hook_event_name": "PostToolUse",
                  {{baseFields}},
                  "tool_name": "Bash",
                  "tool_input": {},
                  "tool_response": "ok",
                  "tool_use_id": "u"
                }
                """,
            "PostToolUseFailure" => $$"""
                {
                  "hook_event_name": "PostToolUseFailure",
                  {{baseFields}},
                  "tool_name": "Bash",
                  "tool_input": {},
                  "tool_use_id": "u",
                  "error": "boom"
                }
                """,
            "UserPromptSubmit" => $$"""
                {
                  "hook_event_name": "UserPromptSubmit",
                  {{baseFields}},
                  "prompt": "hi"
                }
                """,
            "Stop" => $$"""
                {
                  "hook_event_name": "Stop",
                  {{baseFields}},
                  "stop_hook_active": false
                }
                """,
            "SubagentStop" => $$"""
                {
                  "hook_event_name": "SubagentStop",
                  {{baseFields}},
                  "stop_hook_active": false,
                  "agent_id": "a",
                  "agent_transcript_path": "/at",
                  "agent_type": "general"
                }
                """,
            "SubagentStart" => $$"""
                {
                  "hook_event_name": "SubagentStart",
                  {{baseFields}},
                  "agent_id": "a",
                  "agent_type": "general"
                }
                """,
            "PreCompact" => $$"""
                {
                  "hook_event_name": "PreCompact",
                  {{baseFields}},
                  "trigger": "manual"
                }
                """,
            "Notification" => $$"""
                {
                  "hook_event_name": "Notification",
                  {{baseFields}},
                  "message": "msg",
                  "notification_type": "info"
                }
                """,
            "PermissionRequest" => $$"""
                {
                  "hook_event_name": "PermissionRequest",
                  {{baseFields}},
                  "tool_name": "Bash",
                  "tool_input": {}
                }
                """,
            _ => throw new System.ArgumentOutOfRangeException(nameof(discriminator)),
        };
    }
}
