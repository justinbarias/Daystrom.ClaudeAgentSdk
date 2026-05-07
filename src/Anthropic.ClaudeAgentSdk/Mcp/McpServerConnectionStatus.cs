using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>
/// Connection state of an MCP server as reported by the CLI.
/// Mirrors <c>claude_agent_sdk.types.McpServerConnectionStatus</c>.
/// </summary>
public enum McpServerConnectionStatus
{
    /// <summary>Connected and ready.</summary>
    [JsonStringEnumMemberName("connected")]
    Connected,

    /// <summary>Connection failed.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>OAuth/auth handshake required.</summary>
    [JsonStringEnumMemberName("needs-auth")]
    NeedsAuth,

    /// <summary>Connection in progress.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>Server is configured but disabled.</summary>
    [JsonStringEnumMemberName("disabled")]
    Disabled,
}
