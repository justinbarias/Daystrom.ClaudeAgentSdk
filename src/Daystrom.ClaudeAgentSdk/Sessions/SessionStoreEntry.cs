using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Sessions;

/// <summary>
/// One JSONL transcript line as observed by an <c>ISessionStore</c> adapter.
/// The concrete shape is the CLI's on-disk transcript format — a large
/// internal discriminated union — so adapters should treat entries as
/// pass-through blobs. Round-tripping <c>JsonSerializer.Serialize</c> /
/// <c>JsonSerializer.Deserialize</c> is the only required invariant.
/// </summary>
/// <remarks>
/// Mirrors <c>claude_agent_sdk.types.SessionStoreEntry</c> — a Python
/// <c>TypedDict(total=False)</c>. The <c>type</c> field is the only field
/// the SDK reads; everything else is opaque and travels in
/// <see cref="AdditionalProperties"/>.
/// </remarks>
public sealed record SessionStoreEntry
{
    /// <summary>Discriminator for the underlying transcript-line variant.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Stable identifier most entries carry; adapters should treat this as
    /// an idempotency key (upsert / ignore-duplicate). Entries without a
    /// uuid (titles, tags, mode markers) should be appended without dedup.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    /// <summary>ISO-8601 timestamp emitted by the CLI when the entry was written.</summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    /// <summary>
    /// All other fields the CLI emits. Pass-through; adapters must preserve
    /// these byte-for-byte (deep-equal is sufficient — JSON-key reorderings
    /// are tolerated).
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
