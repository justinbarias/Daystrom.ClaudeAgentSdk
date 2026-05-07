using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Agents;

/// <summary>
/// Programmatic sub-agent definition usable as a value of
/// <c>ClaudeAgentOptions.Agents</c>. Mirrors
/// <c>claude_agent_sdk.types.AgentDefinition</c>.
/// </summary>
/// <remarks>
/// Most field names on the wire are camelCase (Python uses
/// <c># noqa: N815</c> for the same reason). The default snake_case
/// policy is overridden per-property here.
/// </remarks>
public sealed record AgentDefinition
{
    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>System prompt body.</summary>
    public required string Prompt { get; init; }

    /// <summary>Tool allow-list for the sub-agent (null inherits the parent set).</summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>Tools explicitly blocked.</summary>
    [JsonPropertyName("disallowedTools")]
    public IReadOnlyList<string>? DisallowedTools { get; init; }

    /// <summary>Model alias or full id (e.g. <c>sonnet</c>, <c>opus</c>, <c>haiku</c>, <c>inherit</c>).</summary>
    public string? Model { get; init; }

    /// <summary>Skills to enable for this sub-agent.</summary>
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>Memory scope (<c>user</c>, <c>project</c>, <c>local</c>) the sub-agent operates in.</summary>
    public string? Memory { get; init; }

    /// <summary>MCP servers to expose. Each entry is either a server name or an inline config.</summary>
    [JsonPropertyName("mcpServers")]
    public IReadOnlyList<object>? McpServers { get; init; }

    /// <summary>Initial prompt to seed the sub-agent's conversation.</summary>
    [JsonPropertyName("initialPrompt")]
    public string? InitialPrompt { get; init; }

    /// <summary>Maximum number of conversation turns.</summary>
    [JsonPropertyName("maxTurns")]
    public int? MaxTurns { get; init; }

    /// <summary>Run as a long-lived background sub-agent.</summary>
    public bool? Background { get; init; }

    /// <summary>Effort hint (<c>low</c>, <c>medium</c>, <c>high</c>, <c>max</c>) or numeric override.</summary>
    public object? Effort { get; init; }

    /// <summary>Permission mode the sub-agent runs under.</summary>
    [JsonPropertyName("permissionMode")]
    public PermissionMode? PermissionMode { get; init; }
}
