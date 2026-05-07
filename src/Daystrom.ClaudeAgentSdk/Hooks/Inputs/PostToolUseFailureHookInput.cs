using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired after a tool fails or is interrupted.</summary>
public sealed record PostToolUseFailureHookInput : HookInput
{
    /// <summary>Tool that was invoked.</summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>Inputs that were passed to the tool.</summary>
    [JsonPropertyName("tool_input")]
    public required JsonElement ToolInput { get; init; }

    /// <summary>Stable identifier of the tool-call.</summary>
    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    /// <summary>Error message describing the failure.</summary>
    public required string Error { get; init; }

    /// <summary>True when the failure was caused by user interrupt.</summary>
    [JsonPropertyName("is_interrupt")]
    public bool? IsInterrupt { get; init; }

    /// <summary>Sub-agent identifier when applicable.</summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; init; }

    /// <summary>Sub-agent type name when applicable.</summary>
    [JsonPropertyName("agent_type")]
    public string? AgentType { get; init; }
}
