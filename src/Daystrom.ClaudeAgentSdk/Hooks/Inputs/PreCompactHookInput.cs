using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired before context compaction runs.</summary>
public sealed record PreCompactHookInput : HookInput
{
    /// <summary>How compaction was triggered (<c>manual</c> or <c>auto</c>).</summary>
    public required string Trigger { get; init; }

    /// <summary>Custom instructions supplied by the user, if any.</summary>
    [JsonPropertyName("custom_instructions")]
    public string? CustomInstructions { get; init; }
}
