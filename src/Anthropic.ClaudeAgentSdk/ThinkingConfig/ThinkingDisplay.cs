using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.ThinkingConfig;

/// <summary>
/// Whether thinking-block text is returned with the response.
/// Opus 4.7+ defaults to <see cref="Omitted"/> (signature only); pass
/// <see cref="Summarized"/> to receive text. Mirrors
/// <c>claude_agent_sdk.types.ThinkingDisplay</c>.
/// </summary>
public enum ThinkingDisplay
{
    /// <summary>Return summarized thinking text.</summary>
    [JsonStringEnumMemberName("summarized")]
    Summarized,

    /// <summary>Omit thinking text (signature only).</summary>
    [JsonStringEnumMemberName("omitted")]
    Omitted,
}
