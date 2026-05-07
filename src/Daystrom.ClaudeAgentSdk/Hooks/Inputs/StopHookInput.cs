using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired when the main agent finishes a turn.</summary>
public sealed record StopHookInput : HookInput
{
    /// <summary>True if a stop hook is currently in flight; used for re-entrancy guards.</summary>
    [JsonPropertyName("stop_hook_active")]
    public required bool StopHookActive { get; init; }
}
