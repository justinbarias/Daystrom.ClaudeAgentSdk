using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Messages;

/// <summary>
/// Emitted by the CLI when rate-limit accounting transitions (e.g. from
/// <see cref="RateLimitStatus.Allowed"/> to
/// <see cref="RateLimitStatus.AllowedWarning"/>). Wire discriminator:
/// <c>rate_limit_event</c>.
/// </summary>
public sealed record RateLimitEvent : Messages.Message
{
    /// <summary>The new rate-limit state.</summary>
    [JsonPropertyName("rate_limit_info")]
    public required RateLimitInfo RateLimitInfo { get; init; }

    /// <summary>Stable per-event identifier.</summary>
    public required string Uuid { get; init; }

    /// <summary>Session identifier the event belongs to.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}
