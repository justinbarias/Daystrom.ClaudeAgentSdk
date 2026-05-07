namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>Information about a tool exposed by an MCP server.</summary>
public sealed record McpToolInfo
{
    /// <summary>Tool name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>Annotations describing the tool's effects.</summary>
    public McpToolAnnotations? Annotations { get; init; }
}
