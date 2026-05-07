using System.Collections.Generic;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>MCP stdio server configuration.</summary>
public sealed record McpStdioServerConfig : McpServerConfig
{
    /// <summary>Executable to spawn.</summary>
    public required string Command { get; init; }

    /// <summary>Command-line arguments.</summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Environment variables to set on the child process.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }
}
