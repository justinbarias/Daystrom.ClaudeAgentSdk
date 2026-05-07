using System.Text.Json;
using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Messages.Content;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// User-turn message. Wire discriminator: <c>user</c>.
/// </summary>
/// <remarks>
/// The CLI nests the actual content under a <c>message</c> object on the
/// wire, so this record exposes <see cref="Message"/> as the wrapper. Use
/// <see cref="Message"/>.<see cref="UserMessageBody.Content"/> to reach the
/// content blocks.
/// </remarks>
public sealed record UserMessage : Messages.Message
{
    /// <summary>Wire wrapper containing the content blocks.</summary>
    [JsonPropertyName("message")]
    public required UserMessageBody Message { get; init; }

    /// <summary>SDK message identifier when present.</summary>
    public string? Uuid { get; init; }

    /// <summary>Identifier of the parent tool-use block when this is a sub-agent message.</summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>Optional raw tool-use result attachment.</summary>
    [JsonPropertyName("tool_use_result")]
    public JsonElement? ToolUseResult { get; init; }

    /// <summary>Session identifier the message belongs to.</summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }
}

/// <summary>
/// Wire wrapper sitting under <see cref="UserMessage.Message"/>. The CLI
/// emits user content in this nested shape; the SDK preserves it rather
/// than flattening so the source-gen serializer round-trips byte-for-byte.
/// </summary>
public sealed record UserMessageBody
{
    /// <summary>
    /// Content of the user turn. The CLI may emit either a list of typed
    /// <see cref="ContentBlock"/>s or a bare string; the bare-string case
    /// arrives as a <see cref="JsonElement"/> with <c>ValueKind = String</c>.
    /// </summary>
    public required JsonElement Content { get; init; }
}
