namespace Anthropic.ClaudeAgentSdk.ThinkingConfig;

/// <summary>
/// Adaptive thinking — Claude decides when and how much to think.
/// Default for models that support it (Opus 4.6+).
/// </summary>
public sealed record ThinkingConfigAdaptive : ThinkingConfig
{
    /// <summary>Whether the SDK receives summarised thinking or only the signature.</summary>
    public ThinkingDisplay? Display { get; init; }
}
