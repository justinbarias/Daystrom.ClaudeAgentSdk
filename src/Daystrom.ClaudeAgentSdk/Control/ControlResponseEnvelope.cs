namespace Daystrom.ClaudeAgentSdk.Control;

/// <summary>
/// Outer envelope for a <c>control_response</c> NDJSON frame. Either a
/// reply to one of our outbound requests, or our reply to an inbound CLI
/// request. The correlation id and success/error subtype live on the
/// nested <see cref="Response"/> payload, mirroring the Python SDK's
/// <c>SDKControlResponse</c>.
/// </summary>
/// <remarks>
/// Wire shape:
/// <c>{ "type": "control_response", "response": { "subtype": "success" | "error", "request_id": "...", "response": {...}? , "error": "..."? } }</c>.
/// See <c>_internal/query.py:425–450</c> for the canonical construction
/// site (success and error branches).
/// </remarks>
internal sealed record ControlResponseEnvelope
{
    /// <summary>Wire <c>type</c> tag; always <c>"control_response"</c>.</summary>
    public string Type { get; init; } = "control_response";

    /// <summary>The response payload; carries success/error and the
    /// correlated request id.</summary>
    public required ControlResponsePayload Response { get; init; }
}
