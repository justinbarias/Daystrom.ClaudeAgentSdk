using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Inbound <c>can_use_tool</c> control request — the CLI is asking
/// whether the user-supplied <c>CanUseTool</c> callback approves a
/// particular tool invocation. The handler returns a
/// <see cref="Permissions.PermissionResult"/>. Mirrors the Python SDK at
/// <c>_internal/query.py:344–387</c>.
/// </summary>
/// <remarks>
/// Wire shape: <c>{ "subtype": "can_use_tool", "tool_name": "Bash", "input": {...},
/// "permission_suggestions": [...]?, "tool_use_id": "...", "agent_id": "..." }</c>.
/// Concrete handler ships in Phase 8 (permissions); Phase 6 only
/// registers this envelope so the dispatcher compiles. See
/// <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/can_use_tool_request.json</c>.
/// </remarks>
internal sealed record CanUseToolRequest : ControlRequestPayload
{
    /// <summary>Tool the CLI wants to invoke (e.g. <c>"Bash"</c>).</summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Raw JSON input the CLI plans to pass to the tool. Phase 8 hands
    /// this through to the user callback as a <see cref="JsonElement"/>;
    /// the SDK does not interpret tool inputs.
    /// </summary>
    public required JsonElement Input { get; init; }

    /// <summary>
    /// Optional CLI-supplied permission suggestions surfaced to the
    /// callback through <c>ToolPermissionContext.Suggestions</c>.
    /// </summary>
    [JsonPropertyName("permission_suggestions")]
    public IReadOnlyList<JsonElement>? PermissionSuggestions { get; init; }

    /// <summary>Tool-use id correlating this request with a tool_use
    /// content block in the conversation.</summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    /// <summary>Agent making the tool call, when invoked from a subagent.</summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; init; }
}
