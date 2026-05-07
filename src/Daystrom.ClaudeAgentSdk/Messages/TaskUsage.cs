using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Usage breakdown carried on <see cref="TaskProgressMessage"/> and
/// <see cref="TaskNotificationMessage"/>.
/// </summary>
public sealed record TaskUsage
{
    /// <summary>Cumulative tokens consumed by the task.</summary>
    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; init; }

    /// <summary>Number of tool invocations performed by the task.</summary>
    [JsonPropertyName("tool_uses")]
    public required int ToolUses { get; init; }

    /// <summary>Wall-clock duration of the task in milliseconds.</summary>
    [JsonPropertyName("duration_ms")]
    public required int DurationMs { get; init; }
}
