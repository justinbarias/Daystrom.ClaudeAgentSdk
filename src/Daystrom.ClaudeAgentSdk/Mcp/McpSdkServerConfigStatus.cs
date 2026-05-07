namespace Daystrom.ClaudeAgentSdk.Mcp;

/// <summary>
/// Output-only SDK MCP server config — returned in status responses.
/// Unlike <see cref="McpSdkServerConfig"/> this carries only the
/// serialisable <see cref="Name"/>, no in-process instance.
/// </summary>
public sealed record McpSdkServerConfigStatus : McpServerStatusConfig
{
    /// <summary>Server name as reported in the status response.</summary>
    public required string Name { get; init; }
}
