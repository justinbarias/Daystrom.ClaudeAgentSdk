using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Outputs;

/// <summary>
/// <see cref="HookSpecificOutput"/> for the <see cref="HookEvent.SubagentStart"/> event.
/// </summary>
public sealed record SubagentStartHookSpecificOutput : HookSpecificOutput
{
    /// <summary>Extra context to inject into the launching sub-agent.</summary>
    [JsonPropertyName("additionalContext")]
    public string? AdditionalContext { get; init; }
}
