using System.Text.Json;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Internal;
using Daystrom.ClaudeAgentSdk.Messages;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Internal;

public class MessageParserTests
{
    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Parse_UserMessage_RoundTrips()
    {
        var element = Parse(Fixtures.Read("message/user.json"));
        var msg = MessageParser.Parse(element);
        Assert.IsType<UserMessage>(msg);
    }

    [Fact]
    public void Parse_AssistantMessage_RoundTrips()
    {
        var element = Parse(Fixtures.Read("message/assistant.json"));
        var msg = MessageParser.Parse(element);
        var assistant = Assert.IsType<AssistantMessage>(msg);
        Assert.Equal("claude-opus-4-1-20250805", assistant.Message.Model);
    }

    [Fact]
    public void Parse_ResultMessage_RoundTrips()
    {
        var element = Parse(Fixtures.Read("message/result_success.json"));
        var msg = MessageParser.Parse(element);
        var result = Assert.IsType<ResultMessage>(msg);
        Assert.Equal("session_123", result.SessionId);
    }

    [Fact]
    public void Parse_StreamEvent_RoundTrips()
    {
        var element = Parse(Fixtures.Read("message/stream_event.json"));
        var msg = MessageParser.Parse(element);
        Assert.IsType<StreamEvent>(msg);
    }

    [Fact]
    public void Parse_RateLimitEvent_RoundTrips()
    {
        var element = Parse(Fixtures.Read("message/rate_limit_event.json"));
        var msg = MessageParser.Parse(element);
        Assert.IsType<RateLimitEvent>(msg);
    }

    [Fact]
    public void Parse_GenericSystemMessage_StaysAsBaseSystemMessage()
    {
        var element = Parse(Fixtures.Read("message/system_generic.json"));
        var msg = MessageParser.Parse(element);
        var system = Assert.IsType<SystemMessage>(msg);
        Assert.Equal("start", system.Subtype);
    }

    [Fact]
    public void Parse_TaskStartedSubtype_SynthesisesTaskStartedMessage()
    {
        var element = Parse(Fixtures.Read("message/system_task_started.json"));
        var msg = MessageParser.Parse(element);
        var task = Assert.IsType<TaskStartedMessage>(msg);
        Assert.Equal("task-abc", task.TaskId);
        Assert.Equal("Reticulating splines", task.Description);
        Assert.Equal("session-1", task.SessionId);
    }

    [Fact]
    public void Parse_TaskProgressSubtype_SynthesisesTaskProgressMessage()
    {
        var element = Parse(Fixtures.Read("message/system_task_progress.json"));
        var msg = MessageParser.Parse(element);
        var task = Assert.IsType<TaskProgressMessage>(msg);
        Assert.Equal(1234, task.Usage.TotalTokens);
        Assert.Equal("Read", task.LastToolName);
    }

    [Fact]
    public void Parse_TaskNotificationSubtype_SynthesisesTaskNotificationMessage()
    {
        var element = Parse(Fixtures.Read("message/system_task_notification.json"));
        var msg = MessageParser.Parse(element);
        var task = Assert.IsType<TaskNotificationMessage>(msg);
        Assert.Equal(TaskNotificationStatus.Completed, task.Status);
    }

    [Fact]
    public void Parse_MirrorErrorSubtype_SynthesisesMirrorErrorMessage()
    {
        var json = """
            {
              "type": "system",
              "subtype": "mirror_error",
              "error": "store unreachable"
            }
            """;
        var element = Parse(json);
        var msg = MessageParser.Parse(element);
        var mirror = Assert.IsType<MirrorErrorMessage>(msg);
        Assert.Equal("store unreachable", mirror.Error);
    }

    [Fact]
    public void Parse_UnknownSystemSubtype_PassesThroughAsBaseSystemMessage()
    {
        var json = """{ "type": "system", "subtype": "future_subtype_unknown" }""";
        var element = Parse(json);
        var msg = MessageParser.Parse(element);
        var system = Assert.IsType<SystemMessage>(msg);
        Assert.Equal("future_subtype_unknown", system.Subtype);
    }

    [Fact]
    public void Parse_UnknownTopLevelType_ReturnsNull()
    {
        // Forward-compat: a CLI newer than the SDK may emit message types
        // we don't know about. They must be skipped silently.
        var json = """{ "type": "future_message_type", "payload": {} }""";
        var element = Parse(json);
        Assert.Null(MessageParser.Parse(element));
    }

    [Fact]
    public void Parse_NonObject_ReturnsNull()
    {
        Assert.Null(MessageParser.Parse(Parse("\"a string\"")));
        Assert.Null(MessageParser.Parse(Parse("42")));
        Assert.Null(MessageParser.Parse(Parse("[]")));
    }

    [Fact]
    public void Parse_MissingTypeField_ReturnsNull()
    {
        var element = Parse("""{ "subtype": "no_type_here" }""");
        Assert.Null(MessageParser.Parse(element));
    }

    [Fact]
    public void Parse_MalformedSchema_ThrowsCliJsonDecodeExceptionWithRawLine()
    {
        // `result` requires several fields; missing them yields a
        // recognised type but a broken schema — exactly the failure mode
        // we want surfaced as CliJsonDecodeException, not JsonException.
        var json = """{ "type": "result", "subtype": "success" }""";
        var element = Parse(json);
        var ex = Assert.Throws<CliJsonDecodeException>(() => MessageParser.Parse(element));
        Assert.Contains("\"type\": \"result\"", ex.RawLine);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public void Parse_TaskSubtypeMissingRequiredField_ThrowsCliJsonDecodeException()
    {
        // task_started requires task_id, description, uuid, session_id.
        // Drop session_id to verify the second-level synthesis path also
        // wraps JsonException correctly.
        var json = """
            {
              "type": "system",
              "subtype": "task_started",
              "task_id": "t",
              "description": "d",
              "uuid": "u"
            }
            """;
        var element = Parse(json);
        var ex = Assert.Throws<CliJsonDecodeException>(() => MessageParser.Parse(element));
        Assert.IsType<JsonException>(ex.InnerException);
    }
}
