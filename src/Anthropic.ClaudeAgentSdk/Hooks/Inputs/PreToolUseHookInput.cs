using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired before a tool is invoked.</summary>
public sealed record PreToolUseHookInput : HookInput
{
    /// <summary>Tool that is about to be invoked.</summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>Inputs the model is passing to the tool.</summary>
    [JsonPropertyName("tool_input")]
    public required JsonElement ToolInput { get; init; }

    /// <summary>Stable identifier of this tool-call within the assistant message.</summary>
    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    /// <summary>Sub-agent identifier when the hook fires inside a sub-agent.</summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; init; }

    /// <summary>Sub-agent type name when applicable.</summary>
    [JsonPropertyName("agent_type")]
    public string? AgentType { get; init; }
}
