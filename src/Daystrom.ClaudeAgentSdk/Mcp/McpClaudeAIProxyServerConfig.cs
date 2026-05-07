namespace Daystrom.ClaudeAgentSdk.Mcp;

/// <summary>
/// Claude.ai proxy MCP server configuration. Output-only — appears in
/// status responses for servers proxied through Claude.ai. Wire
/// discriminator: <c>claudeai-proxy</c> (kebab-case).
/// </summary>
public sealed record McpClaudeAIProxyServerConfig : McpServerStatusConfig
{
    /// <summary>Proxy URL.</summary>
    public required string Url { get; init; }

    /// <summary>Proxy server identifier.</summary>
    public required string Id { get; init; }
}
