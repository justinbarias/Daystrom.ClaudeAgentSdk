using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Sessions;

/// <summary>
/// Incrementally-maintained session summary. Stores obtain this from the
/// SDK's fold helper inside <c>ISessionStore.AppendAsync</c> and persist
/// it verbatim; they return the full set from
/// <c>ISessionStore.ListSessionSummariesAsync</c>.
/// </summary>
/// <remarks>
/// <see cref="Data"/> is opaque SDK-owned state. Stores MUST NOT interpret
/// or mutate it.
/// </remarks>
public sealed record SessionSummaryEntry
{
    /// <summary>Stable session identifier.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Storage write time of the sidecar, in Unix epoch milliseconds. Must
    /// use the same clock source as the <c>mtime</c> returned by
    /// <c>ISessionStore.ListSessionsAsync</c> for this session — typically
    /// file mtime, S3 <c>LastModified</c>, Postgres <c>updated_at</c>, or
    /// whatever native timestamp the adapter surfaces. Do NOT derive this
    /// from entry ISO timestamps.
    /// </summary>
    [JsonPropertyName("mtime")]
    public required long Mtime { get; init; }

    /// <summary>Opaque SDK-owned summary state. Persist verbatim; do not interpret.</summary>
    [JsonPropertyName("data")]
    public required IReadOnlyDictionary<string, JsonElement> Data { get; init; }
}
