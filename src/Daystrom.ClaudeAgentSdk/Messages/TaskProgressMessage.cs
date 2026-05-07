using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// System message emitted while a task is in progress. Sub-discriminator:
/// <c>subtype = "task_progress"</c>.
/// </summary>
public sealed record TaskProgressMessage : SystemMessage
{
    /// <summary>Identifier of the task.</summary>
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    /// <summary>Human-readable description of current activity.</summary>
    public required string Description { get; init; }

    /// <summary>Cumulative usage so far.</summary>
    public required TaskUsage Usage { get; init; }

    /// <summary>Stable per-message identifier.</summary>
    public required string Uuid { get; init; }

    /// <summary>Identifier of the session the task runs inside.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>Tool-use id that spawned the task, when applicable.</summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    /// <summary>Most-recently invoked tool name when set.</summary>
    [JsonPropertyName("last_tool_name")]
    public string? LastToolName { get; init; }
}
