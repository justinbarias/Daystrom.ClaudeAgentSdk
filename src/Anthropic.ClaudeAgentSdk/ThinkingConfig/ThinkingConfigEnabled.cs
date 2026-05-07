using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.ThinkingConfig;

/// <summary>
/// Fixed-budget extended thinking. <see cref="BudgetTokens"/> caps how
/// many tokens the model may spend reasoning before producing output.
/// Used with older models that don't support adaptive thinking.
/// </summary>
public sealed record ThinkingConfigEnabled : ThinkingConfig
{
    /// <summary>Maximum tokens the model may spend on thinking.</summary>
    [JsonPropertyName("budget_tokens")]
    public required int BudgetTokens { get; init; }

    /// <summary>Whether the SDK receives summarised thinking or only the signature.</summary>
    public ThinkingDisplay? Display { get; init; }
}
