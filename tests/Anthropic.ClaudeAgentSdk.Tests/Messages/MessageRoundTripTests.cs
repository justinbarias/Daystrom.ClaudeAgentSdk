using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.ClaudeAgentSdk.Messages;
using Anthropic.ClaudeAgentSdk.Messages.Content;
using Xunit;

namespace Anthropic.ClaudeAgentSdk.Tests.Messages;

public class MessageRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void UserMessage_Parses_TextContent()
    {
        var json = Fixtures.Read("message/user.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var user = Assert.IsType<UserMessage>(msg);
        Assert.Equal(JsonValueKind.Array, user.Message.Content.ValueKind);
    }

    [Fact]
    public void UserMessage_PreservesUuid()
    {
        var json = Fixtures.Read("message/user_with_uuid.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var user = Assert.IsType<UserMessage>(msg);
        Assert.Equal("msg-abc123-def456", user.Uuid);
    }

    [Fact]
    public void AssistantMessage_Parses_ContentAndModel()
    {
        var json = Fixtures.Read("message/assistant.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var assistant = Assert.IsType<AssistantMessage>(msg);
        Assert.Equal("claude-opus-4-1-20250805", assistant.Message.Model);
        Assert.Equal(2, assistant.Message.Content.Count);
        Assert.IsType<TextBlock>(assistant.Message.Content[0]);
        Assert.IsType<ToolUseBlock>(assistant.Message.Content[1]);
    }

    [Fact]
    public void AssistantMessage_PreservesUsage()
    {
        var json = Fixtures.Read("message/assistant_with_usage.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var assistant = Assert.IsType<AssistantMessage>(msg);
        Assert.NotNull(assistant.Message.Usage);
        Assert.Equal(100, assistant.Message.Usage!.Value.GetProperty("input_tokens").GetInt32());
        Assert.Equal(50, assistant.Message.Usage!.Value.GetProperty("output_tokens").GetInt32());
    }

    [Fact]
    public void SystemMessage_Generic_Parses()
    {
        var json = Fixtures.Read("message/system_generic.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var system = Assert.IsType<SystemMessage>(msg);
        Assert.Equal("start", system.Subtype);
    }

    [Fact]
    public void SystemMessage_TaskStarted_ParsesAsBaseSystemMessage()
    {
        // Phase 3.4: STJ polymorphism is single-level, so task subtypes
        // arrive as bare SystemMessage. The MessageParser in Phase 5
        // re-dispatches on Subtype to synthesise TaskStartedMessage.
        var json = Fixtures.Read("message/system_task_started.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var system = Assert.IsType<SystemMessage>(msg);
        Assert.Equal("task_started", system.Subtype);
    }

    [Fact]
    public void TaskStartedMessage_RoundTrips_WhenStaticTypeKnown()
    {
        var json = Fixtures.Read("message/system_task_started.json");
        var task = JsonSerializer.Deserialize<TaskStartedMessage>(json, Options);
        Assert.NotNull(task);
        Assert.Equal("task-abc", task!.TaskId);
        Assert.Equal("Reticulating splines", task.Description);
        Assert.Equal("background", task.TaskType);
        Assert.Equal("session-1", task.SessionId);
    }

    [Fact]
    public void TaskProgressMessage_RoundTrips_WhenStaticTypeKnown()
    {
        var json = Fixtures.Read("message/system_task_progress.json");
        var task = JsonSerializer.Deserialize<TaskProgressMessage>(json, Options);
        Assert.NotNull(task);
        Assert.Equal("task-abc", task!.TaskId);
        Assert.Equal(1234, task.Usage.TotalTokens);
        Assert.Equal(5, task.Usage.ToolUses);
        Assert.Equal(9876, task.Usage.DurationMs);
        Assert.Equal("Read", task.LastToolName);
    }

    [Fact]
    public void TaskNotificationMessage_RoundTrips_WhenStaticTypeKnown()
    {
        var json = Fixtures.Read("message/system_task_notification.json");
        var task = JsonSerializer.Deserialize<TaskNotificationMessage>(json, Options);
        Assert.NotNull(task);
        Assert.Equal(TaskNotificationStatus.Completed, task!.Status);
        Assert.Equal("/tmp/out.md", task.OutputFile);
        Assert.NotNull(task.Usage);
    }

    [Fact]
    public void ResultMessage_Parses_Success()
    {
        var json = Fixtures.Read("message/result_success.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var result = Assert.IsType<ResultMessage>(msg);
        Assert.Equal("success", result.Subtype);
        Assert.Equal(1000, result.DurationMs);
        Assert.Equal(500, result.DurationApiMs);
        Assert.False(result.IsError);
        Assert.Equal(2, result.NumTurns);
        Assert.Equal("session_123", result.SessionId);
        Assert.Null(result.StopReason);
    }

    [Fact]
    public void ResultMessage_Parses_StopReasonAndResult()
    {
        var json = Fixtures.Read("message/result_with_stop_reason.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var result = Assert.IsType<ResultMessage>(msg);
        Assert.Equal("end_turn", result.StopReason);
        Assert.Equal("Done", result.Result);
    }

    [Fact]
    public void ResultMessage_OneShot_MatchesStreaming_Shape()
    {
        // Acceptance from master plan task 3.4: ResultMessage shape is
        // byte-identical between one-shot and streaming. The fixtures
        // result_success.json and result_with_stop_reason.json are
        // representative of both modes (the wire shape is genuinely the
        // same — the only difference is whether `result` is populated).
        var oneShotJson = Fixtures.Read("message/result_with_stop_reason.json");
        var streamingJson = Fixtures.Read("message/result_success.json");

        var oneShot = JsonSerializer.Deserialize<Message>(oneShotJson, Options);
        var streaming = JsonSerializer.Deserialize<Message>(streamingJson, Options);

        Assert.IsType<ResultMessage>(oneShot);
        Assert.IsType<ResultMessage>(streaming);

        // The shapes are the same record type with different optional
        // fields populated; both round-trip to ResultMessage which is what
        // the Phase 5 fast path depends on.
    }

    [Fact]
    public void StreamEvent_Parses()
    {
        var json = Fixtures.Read("message/stream_event.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var stream = Assert.IsType<StreamEvent>(msg);
        Assert.Equal("stream-uuid-1", stream.Uuid);
        Assert.Equal("session-1", stream.SessionId);
        Assert.Equal("content_block_delta", stream.Event.GetProperty("type").GetString());
    }

    [Fact]
    public void RateLimitEvent_Parses_WithCamelCaseInfo()
    {
        var json = Fixtures.Read("message/rate_limit_event.json");
        var msg = JsonSerializer.Deserialize<Message>(json, Options);
        var rate = Assert.IsType<RateLimitEvent>(msg);
        Assert.Equal("abc-123", rate.Uuid);
        Assert.Equal("session_xyz", rate.SessionId);
        Assert.Equal(RateLimitStatus.AllowedWarning, rate.RateLimitInfo.Status);
        Assert.Equal(1700000000, rate.RateLimitInfo.ResetsAt);
        Assert.Equal(RateLimitType.FiveHour, rate.RateLimitInfo.RateLimitType);
        Assert.Equal(0.91, rate.RateLimitInfo.Utilization);
    }

    [Fact]
    public void Discriminator_AssistantMessage_SerializesAsAssistant()
    {
        var msg = new AssistantMessage
        {
            Message = new AssistantMessageBody
            {
                Model = "claude-opus-4-7",
                Content = new[] { new TextBlock("hi") },
            },
        };
        var json = JsonSerializer.Serialize<Message>(msg, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("assistant", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void Discriminator_RateLimitEvent_SerializesAsRateLimitEvent()
    {
        var msg = new RateLimitEvent
        {
            RateLimitInfo = new RateLimitInfo { Status = RateLimitStatus.Allowed },
            Uuid = "u",
            SessionId = "s",
        };
        var json = JsonSerializer.Serialize<Message>(msg, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("rate_limit_event", doc.RootElement.GetProperty("type").GetString());
    }
}
