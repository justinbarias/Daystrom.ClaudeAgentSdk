namespace Daystrom.ClaudeAgentSdk.Control;

/// <summary>
/// Outer envelope for a <c>control_cancel_request</c> NDJSON frame —
/// emitted by the CLI to cancel an in-flight inbound control request the
/// SDK has not yet responded to. The receiver looks up
/// <see cref="RequestId"/> in its in-flight handler table and cancels it.
/// </summary>
/// <remarks>
/// <para>Wire shape: <c>{ "type": "control_cancel_request", "request_id": "..." }</c>.</para>
/// <para>Routed at <c>_internal/query.py:272–278</c>.</para>
/// </remarks>
internal sealed record ControlCancelRequestEnvelope
{
    /// <summary>Wire <c>type</c> tag; always <c>"control_cancel_request"</c>.</summary>
    public string Type { get; init; } = "control_cancel_request";

    /// <summary>
    /// Identifier of the inbound control request to cancel. Matches a
    /// previously-received <see cref="ControlRequestEnvelope.RequestId"/>.
    /// </summary>
    public required string RequestId { get; init; }
}
