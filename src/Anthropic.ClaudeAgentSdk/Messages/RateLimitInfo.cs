using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Messages;

/// <summary>
/// Rate-limit accounting payload reported by the CLI. Most fields use
/// <b>camelCase</b> on the wire even though the rest of the protocol is
/// snake_case — these need explicit <see cref="JsonPropertyNameAttribute"/>
/// overrides so the source-gen <c>SnakeCaseLower</c> policy doesn't mangle
/// them.
/// </summary>
public sealed record RateLimitInfo
{
    /// <summary>Current rate-limit status.</summary>
    public required RateLimitStatus Status { get; init; }

    /// <summary>Unix timestamp (seconds) when the limit window resets.</summary>
    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; init; }

    /// <summary>Which rate-limit window applies.</summary>
    [JsonPropertyName("rateLimitType")]
    public RateLimitType? RateLimitType { get; init; }

    /// <summary>Fraction of the rate limit consumed (0.0–1.0).</summary>
    public double? Utilization { get; init; }

    /// <summary>Status of overage / pay-as-you-go usage if applicable.</summary>
    [JsonPropertyName("overageStatus")]
    public RateLimitStatus? OverageStatus { get; init; }

    /// <summary>Unix timestamp when the overage window resets.</summary>
    [JsonPropertyName("overageResetsAt")]
    public long? OverageResetsAt { get; init; }

    /// <summary>Why overage is unavailable when <see cref="OverageStatus"/> is rejected.</summary>
    [JsonPropertyName("overageDisabledReason")]
    public string? OverageDisabledReason { get; init; }
}
