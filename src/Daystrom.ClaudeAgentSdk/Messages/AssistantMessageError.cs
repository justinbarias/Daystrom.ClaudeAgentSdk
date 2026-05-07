using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Error category surfaced on an <c>AssistantMessage</c> when the
/// CLI was unable to deliver the assistant turn. Mirrors
/// <c>claude_agent_sdk.types.AssistantMessageError</c>.
/// </summary>
public enum AssistantMessageError
{
    /// <summary>The API rejected the credentials.</summary>
    [JsonStringEnumMemberName("authentication_failed")]
    AuthenticationFailed,

    /// <summary>Billing-related failure (e.g. payment required).</summary>
    [JsonStringEnumMemberName("billing_error")]
    BillingError,

    /// <summary>Hit a rate limit.</summary>
    [JsonStringEnumMemberName("rate_limit")]
    RateLimit,

    /// <summary>Request was malformed or rejected as invalid by the API.</summary>
    [JsonStringEnumMemberName("invalid_request")]
    InvalidRequest,

    /// <summary>Generic server-side failure.</summary>
    [JsonStringEnumMemberName("server_error")]
    ServerError,

    /// <summary>Unclassified error.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
