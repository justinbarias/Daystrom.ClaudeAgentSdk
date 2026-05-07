using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Sessions;

/// <summary>
/// Entry returned by <c>ISessionStore.ListSessionsAsync</c>. Lightweight
/// session-id + last-modified pair used to drive sort-by-recency listings.
/// </summary>
public sealed record SessionStoreListEntry
{
    /// <summary>Stable session identifier.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Last-modified time in Unix epoch milliseconds. Adapters without a
    /// native modification time (e.g. Redis) must maintain their own index.
    /// </summary>
    [JsonPropertyName("mtime")]
    public required long Mtime { get; init; }
}
