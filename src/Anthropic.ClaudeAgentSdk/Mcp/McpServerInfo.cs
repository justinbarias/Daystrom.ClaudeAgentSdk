namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>Server-info payload from an MCP initialize handshake.</summary>
public sealed record McpServerInfo
{
    /// <summary>Server name as reported by the MCP server.</summary>
    public required string Name { get; init; }

    /// <summary>Server version string.</summary>
    public required string Version { get; init; }
}
