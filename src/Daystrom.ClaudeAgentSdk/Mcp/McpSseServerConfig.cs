using System.Collections.Generic;

namespace Daystrom.ClaudeAgentSdk.Mcp;

/// <summary>MCP server-sent-events server configuration.</summary>
public sealed record McpSseServerConfig : McpServerConfig
{
    /// <summary>SSE endpoint to connect to.</summary>
    public required string Url { get; init; }

    /// <summary>Optional HTTP headers.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
