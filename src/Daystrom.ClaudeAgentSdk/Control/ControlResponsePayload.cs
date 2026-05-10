using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Control;

/// <summary>
/// Inner payload of a <see cref="ControlResponseEnvelope"/>. Carries the
/// success/error <see cref="Subtype"/>, the correlated
/// <see cref="RequestId"/>, and either a typed <see cref="Response"/>
/// body (on success) or an <see cref="Error"/> string (on error).
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the Python SDK's <c>SDKControlResponse.response</c> dict shape
/// at <c>_internal/query.py:425–450</c>. There is no second-level
/// polymorphism on <see cref="Response"/>: the caller decodes the body
/// per-request-kind because each outbound subtype has a distinct response
/// schema (e.g. <c>mcp_status</c> → <c>McpStatusResponse</c>,
/// <c>get_context_usage</c> → <c>ContextUsageResponse</c>). Phase 6
/// surfaces the body as a raw <see cref="JsonElement"/>; the Phase 6.4
/// client typed-deserialises through
/// <c>ClaudeAgentJsonContext.Default</c>.
/// </para>
/// </remarks>
internal sealed record ControlResponsePayload
{
    /// <summary>
    /// Outcome discriminator: <c>"success"</c> or <c>"error"</c>. (Note:
    /// this is NOT the wire-side <c>[JsonPolymorphic]</c> discriminator —
    /// the Python SDK keeps both branches in one shape.)
    /// </summary>
    public required string Subtype { get; init; }

    /// <summary>The request id this response correlates to.</summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Success body. Schema depends on the originating request subtype;
    /// the caller deserialises through the source-gen context. Null on
    /// error responses.
    /// </summary>
    public JsonElement? Response { get; init; }

    /// <summary>Error message; populated when <see cref="Subtype"/> is
    /// <c>"error"</c>, null on success.</summary>
    public string? Error { get; init; }
}
