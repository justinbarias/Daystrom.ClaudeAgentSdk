using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Which rate-limit window applies to a given <c>RateLimitInfo</c>.
/// Mirrors <c>claude_agent_sdk.types.RateLimitType</c>. See
/// https://docs.claude.com/en/docs/claude-code/rate-limits.
/// </summary>
public enum RateLimitType
{
    /// <summary>Five-hour rolling window.</summary>
    [JsonStringEnumMemberName("five_hour")]
    FiveHour,

    /// <summary>Seven-day rolling window.</summary>
    [JsonStringEnumMemberName("seven_day")]
    SevenDay,

    /// <summary>Seven-day rolling window for Opus models.</summary>
    [JsonStringEnumMemberName("seven_day_opus")]
    SevenDayOpus,

    /// <summary>Seven-day rolling window for Sonnet models.</summary>
    [JsonStringEnumMemberName("seven_day_sonnet")]
    SevenDaySonnet,

    /// <summary>Pay-as-you-go overage window.</summary>
    [JsonStringEnumMemberName("overage")]
    Overage,
}
