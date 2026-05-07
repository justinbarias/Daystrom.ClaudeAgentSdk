using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Partial-message streaming event. Wire discriminator: <c>stream_event</c>.
/// </summary>
/// <remarks>
/// <see cref="Event"/> is the raw Anthropic API stream event (e.g.
/// <c>content_block_start</c>, <c>content_block_delta</c>, <c>message_stop</c>).
/// The shape is opaque at this layer — consumers that care navigate the
/// <see cref="JsonElement"/>.
/// </remarks>
public sealed record StreamEvent : Messages.Message
{
    /// <summary>Stable per-event identifier.</summary>
    public required string Uuid { get; init; }

    /// <summary>Session identifier the event belongs to.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>Raw Anthropic API stream event.</summary>
    public required JsonElement Event { get; init; }

    /// <summary>Identifier of the parent tool-use block when this stream belongs to a sub-agent.</summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }
}
