using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Sessions;

/// <summary>
/// A user or assistant message from a session transcript. Returned by the
/// SDK's transcript-reading API. Fields match the SDK wire-protocol types
/// (<c>SDKUserMessage</c> / <c>SDKAssistantMessage</c>).
/// </summary>
public sealed record SessionMessage
{
    /// <summary>Message role: <c>"user"</c> or <c>"assistant"</c>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Unique message identifier.</summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }

    /// <summary>Identifier of the session this message belongs to.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Raw Anthropic API message payload (role, content, etc.). Held as a
    /// <see cref="JsonElement"/> because the inner shape is the upstream
    /// API model rather than an SDK type.
    /// </summary>
    [JsonPropertyName("message")]
    public required JsonElement Message { get; init; }

    /// <summary>
    /// Always <c>null</c> for top-level conversation messages — tool-use
    /// sidechain messages are filtered out of session reads.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }
}
