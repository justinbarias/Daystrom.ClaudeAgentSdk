using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// System message emitted when a task starts. Sub-discriminator:
/// <c>subtype = "task_started"</c>. Synthesised by the message parser
/// (Phase 5); does not auto-deserialise from the
/// <see cref="Messages.Message"/> polymorphic root.
/// </summary>
public sealed record TaskStartedMessage : SystemMessage
{
    /// <summary>Identifier of the task.</summary>
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    /// <summary>Human-readable description of what the task is doing.</summary>
    public required string Description { get; init; }

    /// <summary>Stable per-message identifier.</summary>
    public required string Uuid { get; init; }

    /// <summary>Identifier of the session the task runs inside.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>Tool-use id that spawned the task, when applicable.</summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    /// <summary>Task category (e.g. <c>background</c>) when set.</summary>
    [JsonPropertyName("task_type")]
    public string? TaskType { get; init; }
}
