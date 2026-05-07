using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Mcp;

/// <summary>
/// Status of one MCP server connection. Returned by
/// <c>IClaudeAgentClient.GetMcpStatusAsync</c> in the
/// <see cref="McpStatusResponse.McpServers"/> list.
/// </summary>
public sealed record McpServerStatus
{
    /// <summary>Server name as configured.</summary>
    public required string Name { get; init; }

    /// <summary>Current connection status.</summary>
    public required McpServerConnectionStatus Status { get; init; }

    /// <summary>Server-info payload from the MCP handshake (when connected).</summary>
    [JsonPropertyName("serverInfo")]
    public McpServerInfo? ServerInfo { get; init; }

    /// <summary>Error message when <see cref="Status"/> is <c>failed</c>.</summary>
    public string? Error { get; init; }

    /// <summary>Server configuration — output-only union, may include claudeai-proxy.</summary>
    public McpServerStatusConfig? Config { get; init; }

    /// <summary>Configuration scope (e.g. <c>project</c>, <c>user</c>, <c>local</c>, <c>claudeai</c>, <c>managed</c>).</summary>
    public string? Scope { get; init; }

    /// <summary>Tools exposed by this server when connected.</summary>
    public IReadOnlyList<McpToolInfo>? Tools { get; init; }
}
