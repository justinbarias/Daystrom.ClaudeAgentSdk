using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired when a sub-agent finishes its task.</summary>
public sealed record SubagentStopHookInput : HookInput
{
    /// <summary>True if a stop hook is currently in flight.</summary>
    [JsonPropertyName("stop_hook_active")]
    public required bool StopHookActive { get; init; }

    /// <summary>Identifier of the sub-agent that stopped.</summary>
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }

    /// <summary>Path to the sub-agent's transcript file.</summary>
    [JsonPropertyName("agent_transcript_path")]
    public required string AgentTranscriptPath { get; init; }

    /// <summary>Sub-agent type name.</summary>
    [JsonPropertyName("agent_type")]
    public required string AgentType { get; init; }
}
