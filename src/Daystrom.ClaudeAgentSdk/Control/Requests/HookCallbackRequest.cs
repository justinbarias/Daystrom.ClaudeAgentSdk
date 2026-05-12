using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Inbound <c>hook_callback</c> control request — the CLI is firing a
/// hook the SDK registered during <c>initialize</c>. The handler resolves
/// <see cref="CallbackId"/> against the local registration table and
/// invokes the user's <c>HookHandler</c> delegate. Mirrors the Python SDK
/// at <c>_internal/query.py:389–403</c>.
/// </summary>
/// <remarks>
/// Wire shape:
/// <c>{ "subtype": "hook_callback", "callback_id": "hook_3", "input": {...}, "tool_use_id": "..." }</c>.
/// The <see cref="Input"/> body is opaque at this layer; Phase 7
/// deserializes it to the matching <c>HookInput</c> variant per
/// <c>HookEvent</c>. Concrete handler ships in Phase 7. See
/// <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/hook_callback_request.json</c>.
/// </remarks>
internal sealed record HookCallbackRequest : ControlRequestPayload
{
    /// <summary>Identifier the SDK assigned to the registered hook
    /// during <c>initialize</c> (format: <c>hook_{counter}</c>).</summary>
    [JsonPropertyName("callback_id")]
    public required string CallbackId { get; init; }

    /// <summary>
    /// Raw hook input. Shape depends on <c>hook_event_name</c> inside
    /// the payload — Phase 7's hook dispatcher reads
    /// <c>hook_event_name</c> and deserializes accordingly.
    /// </summary>
    public required JsonElement Input { get; init; }

    /// <summary>Tool-use id when the hook fires for a particular tool
    /// invocation; null for non-tool events.</summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }
}
