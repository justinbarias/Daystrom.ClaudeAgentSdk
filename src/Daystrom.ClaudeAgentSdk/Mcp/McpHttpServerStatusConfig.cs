using System.Collections.Generic;

namespace Daystrom.ClaudeAgentSdk.Mcp;

/// <summary>
/// Output-only HTTP server config (status responses). See
/// <see cref="McpServerStatusConfig"/> for the shared union.
/// </summary>
public sealed record McpHttpServerStatusConfig : McpServerStatusConfig
{
    /// <summary>HTTP endpoint.</summary>
    public required string Url { get; init; }

    /// <summary>Optional headers.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
