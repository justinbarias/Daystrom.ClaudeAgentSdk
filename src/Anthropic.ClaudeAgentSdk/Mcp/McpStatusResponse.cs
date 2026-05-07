using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>Response shape for <c>IClaudeAgentClient.GetMcpStatusAsync</c>.</summary>
public sealed record McpStatusResponse
{
    /// <summary>Status of each configured MCP server.</summary>
    [JsonPropertyName("mcpServers")]
    public required IReadOnlyList<McpServerStatus> McpServers { get; init; }
}
