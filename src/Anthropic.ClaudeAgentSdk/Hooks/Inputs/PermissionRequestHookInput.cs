using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired before a permission prompt is raised to the user.</summary>
public sealed record PermissionRequestHookInput : HookInput
{
    /// <summary>Tool the permission applies to.</summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>Inputs the model would have passed to the tool.</summary>
    [JsonPropertyName("tool_input")]
    public required JsonElement ToolInput { get; init; }

    /// <summary>Suggested permission updates to apply, if any.</summary>
    [JsonPropertyName("permission_suggestions")]
    public IReadOnlyList<JsonElement>? PermissionSuggestions { get; init; }

    /// <summary>Sub-agent identifier when the request fires inside a sub-agent.</summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; init; }

    /// <summary>Sub-agent type name when applicable.</summary>
    [JsonPropertyName("agent_type")]
    public string? AgentType { get; init; }
}
