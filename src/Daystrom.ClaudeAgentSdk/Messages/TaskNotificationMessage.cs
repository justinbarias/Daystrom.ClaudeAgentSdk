using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// System message emitted when a task completes, fails, or is stopped.
/// Sub-discriminator: <c>subtype = "task_notification"</c>.
/// </summary>
public sealed record TaskNotificationMessage : SystemMessage
{
    /// <summary>Identifier of the task.</summary>
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    /// <summary>Terminal state.</summary>
    public required TaskNotificationStatus Status { get; init; }

    /// <summary>Path to the captured output file.</summary>
    [JsonPropertyName("output_file")]
    public required string OutputFile { get; init; }

    /// <summary>Brief textual summary of what the task did.</summary>
    public required string Summary { get; init; }

    /// <summary>Stable per-message identifier.</summary>
    public required string Uuid { get; init; }

    /// <summary>Identifier of the session the task ran inside.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>Tool-use id that spawned the task, when applicable.</summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    /// <summary>Final usage totals when reported.</summary>
    public TaskUsage? Usage { get; init; }
}
