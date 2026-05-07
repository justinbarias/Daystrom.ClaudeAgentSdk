using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>
/// Tool annotations as reported in MCP server status. Wire fields are
/// camelCase. Mirrors <c>claude_agent_sdk.types.McpToolAnnotations</c>.
/// </summary>
public sealed record McpToolAnnotations
{
    /// <summary>True when the tool only reads (does not mutate state).</summary>
    [JsonPropertyName("readOnly")]
    public bool? ReadOnly { get; init; }

    /// <summary>True when the tool can destroy data.</summary>
    public bool? Destructive { get; init; }

    /// <summary>True when the tool reaches outside the local environment.</summary>
    [JsonPropertyName("openWorld")]
    public bool? OpenWorld { get; init; }
}
