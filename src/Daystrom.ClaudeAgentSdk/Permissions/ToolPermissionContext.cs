using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;

namespace Daystrom.ClaudeAgentSdk.Permissions;

/// <summary>
/// Context passed to <see cref="CanUseToolDelegate"/> callbacks.
/// Mirrors <c>claude_agent_sdk.types.ToolPermissionContext</c>.
/// </summary>
public sealed record ToolPermissionContext
{
    /// <summary>
    /// Reserved for future abort-signal support. Use the
    /// <see cref="CancellationToken"/> argument on
    /// <see cref="CanUseToolDelegate"/> instead.
    /// </summary>
    [JsonIgnore]
    public CancellationToken? Signal { get; init; }

    /// <summary>Permission suggestions surfaced from the CLI alongside the request.</summary>
    public IReadOnlyList<PermissionUpdate> Suggestions { get; init; } = [];

    /// <summary>Stable identifier of the tool call this permission decision applies to.</summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    /// <summary>Sub-agent identifier when the request fires inside a sub-agent.</summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; init; }
}
