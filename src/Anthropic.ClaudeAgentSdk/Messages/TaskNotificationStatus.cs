using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Messages;

/// <summary>
/// Terminal state reported by a <c>TaskNotificationMessage</c>.
/// Mirrors <c>claude_agent_sdk.types.TaskNotificationStatus</c>.
/// </summary>
public enum TaskNotificationStatus
{
    /// <summary>Task ran to completion.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>Task failed with an error.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Task was stopped (e.g. interrupted by the user).</summary>
    [JsonStringEnumMemberName("stopped")]
    Stopped,
}
