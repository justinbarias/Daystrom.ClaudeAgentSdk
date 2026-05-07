using System.Collections.Generic;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>
/// Output-only stdio server config — appears in
/// <see cref="McpServerStatus.Config"/> for stdio-configured servers.
/// Mirrors <see cref="McpStdioServerConfig"/> field-for-field; declared
/// separately because STJ doesn't allow sharing a derived type across
/// two polymorphic bases.
/// </summary>
public sealed record McpStdioServerStatusConfig : McpServerStatusConfig
{
    /// <summary>Executable spawned by the CLI.</summary>
    public required string Command { get; init; }

    /// <summary>Command-line arguments.</summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Environment variables.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }
}
