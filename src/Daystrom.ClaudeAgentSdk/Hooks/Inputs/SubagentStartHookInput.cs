using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired when a sub-agent is launched (via the Task tool).</summary>
public sealed record SubagentStartHookInput : HookInput
{
    /// <summary>Identifier of the new sub-agent.</summary>
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }

    /// <summary>Sub-agent type name.</summary>
    [JsonPropertyName("agent_type")]
    public required string AgentType { get; init; }
}
