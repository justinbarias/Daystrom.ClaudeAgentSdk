using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk;

/// <summary>
/// Anthropic API beta features that can be enabled per session.
/// Mirrors <c>claude_agent_sdk.types.SdkBeta</c>. See
/// https://docs.anthropic.com/en/api/beta-headers.
/// </summary>
public enum SdkBeta
{
    /// <summary>1M-token context window for Sonnet 4 / 4.5.</summary>
    [JsonStringEnumMemberName("context-1m-2025-08-07")]
    Context1m20250807,
}
