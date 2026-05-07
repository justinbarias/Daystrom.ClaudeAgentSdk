using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Hooks.Outputs;

/// <summary>
/// <see cref="HookSpecificOutput"/> for the
/// <see cref="HookEvent.PostToolUseFailure"/> event.
/// </summary>
public sealed record PostToolUseFailureHookSpecificOutput : HookSpecificOutput
{
    /// <summary>Extra context to add when reporting the failure.</summary>
    [JsonPropertyName("additionalContext")]
    public string? AdditionalContext { get; init; }
}
