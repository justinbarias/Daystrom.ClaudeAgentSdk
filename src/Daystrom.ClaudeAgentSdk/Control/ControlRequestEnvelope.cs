namespace Daystrom.ClaudeAgentSdk.Control;

/// <summary>
/// Outer envelope for an outbound or inbound <c>control_request</c> NDJSON
/// frame. Carries a unique <see cref="RequestId"/> the responder echoes in
/// the matching <see cref="ControlResponseEnvelope"/>, and a
/// discriminated <see cref="Request"/> payload selecting the request kind.
/// </summary>
/// <remarks>
/// <para>
/// Wire shape:
/// <c>{ "type": "control_request", "request_id": "req_1_abcd", "request": { "subtype": "...", ... } }</c>
/// </para>
/// <para>
/// Mirrors the Python SDK's <c>SDKControlRequest</c> as constructed in
/// <c>_internal/query.py:473–477</c>. Inbound dispatch reads the outer
/// <c>type</c> field by hand; the envelope itself is not
/// <c>[JsonPolymorphic]</c> because <see cref="ControlResponseEnvelope"/>
/// and <see cref="ControlCancelRequestEnvelope"/> use different shapes.
/// </para>
/// </remarks>
internal sealed record ControlRequestEnvelope
{
    /// <summary>Wire <c>type</c> tag; always <c>"control_request"</c>.</summary>
    public string Type { get; init; } = "control_request";

    /// <summary>
    /// Unique request identifier; outbound requests use the format
    /// <c>req_{counter}_{hex}</c> (see Python <c>_send_control_request</c>),
    /// inbound requests carry the CLI-assigned id and must be echoed in the
    /// response.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>Discriminated payload selecting the request kind.</summary>
    public required ControlRequestPayload Request { get; init; }
}
