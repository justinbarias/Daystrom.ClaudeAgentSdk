using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>
/// In-process MCP server configuration. The wire shape carries only the
/// <see cref="Name"/>; the actual <see cref="Instance"/> is held locally
/// and routed through the control protocol when the CLI sends an
/// <c>mcp_message</c> targeted at this server.
/// </summary>
public sealed record McpSdkServerConfig : McpServerConfig
{
    /// <summary>Server name surfaced to the CLI.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Live in-process MCP server instance. Not serialised — only the
    /// name is sent to the CLI. The control-protocol layer dispatches
    /// inbound MCP messages to this instance.
    /// </summary>
    /// <remarks>
    /// Not marked <c>required</c> because STJ rejects required+ignored
    /// properties; callers configuring an SDK MCP server must set this
    /// to a non-null instance before passing the config to
    /// <c>ClaudeAgentOptions</c>. The Phase 4 options validator throws
    /// if it's null at session-start time.
    /// </remarks>
    [JsonIgnore]
    public IMcpServerInstance Instance { get; init; } = null!;
}
