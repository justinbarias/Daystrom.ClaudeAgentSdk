using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Rate-limit state reported by the CLI when its rate-limit accounting
/// transitions. Mirrors <c>claude_agent_sdk.types.RateLimitStatus</c>.
/// </summary>
public enum RateLimitStatus
{
    /// <summary>Within rate limit.</summary>
    [JsonStringEnumMemberName("allowed")]
    Allowed,

    /// <summary>Approaching rate limit.</summary>
    [JsonStringEnumMemberName("allowed_warning")]
    AllowedWarning,

    /// <summary>Rate limit hit; requests rejected.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,
}
