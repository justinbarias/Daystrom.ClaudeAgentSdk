using System.Collections.Generic;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>MCP HTTP-streaming server configuration.</summary>
public sealed record McpHttpServerConfig : McpServerConfig
{
    /// <summary>HTTP endpoint to connect to.</summary>
    public required string Url { get; init; }

    /// <summary>Optional HTTP headers (e.g. for auth).</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
