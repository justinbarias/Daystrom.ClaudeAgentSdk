using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.ClaudeAgentSdk.Messages.Content;

namespace Anthropic.ClaudeAgentSdk.Messages;

/// <summary>
/// Assistant-turn message. Wire discriminator: <c>assistant</c>.
/// </summary>
/// <remarks>
/// Like <see cref="UserMessage"/>, the wire shape nests model/content/usage
/// under a <c>message</c> object. Reach those via <see cref="Message"/>.
/// </remarks>
public sealed record AssistantMessage : Messages.Message
{
    /// <summary>Wire wrapper containing the content blocks, model id, and usage.</summary>
    [JsonPropertyName("message")]
    public required AssistantMessageBody Message { get; init; }

    /// <summary>SDK message identifier when present.</summary>
    public string? Uuid { get; init; }

    /// <summary>Session identifier the message belongs to.</summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>Identifier of the parent tool-use block when this is a sub-agent message.</summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>Error category surfaced when the assistant turn could not be delivered.</summary>
    public AssistantMessageError? Error { get; init; }
}

/// <summary>
/// Wire wrapper for <see cref="AssistantMessage.Message"/>. Carries the
/// fields the Anthropic API itself produced (model, content, per-turn
/// usage) as opposed to the SDK-level metadata at the parent level.
/// </summary>
public sealed record AssistantMessageBody
{
    /// <summary>Model id that produced the turn (e.g. <c>claude-opus-4-1-20250805</c>).</summary>
    public required string Model { get; init; }

    /// <summary>Content blocks emitted by the assistant.</summary>
    public required IReadOnlyList<ContentBlock> Content { get; init; }

    /// <summary>API-side message id when present.</summary>
    public string? Id { get; init; }

    /// <summary>API-side stop reason when present (<c>end_turn</c>, <c>max_tokens</c>, …).</summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    /// <summary>
    /// Per-turn usage breakdown as raw JSON. Includes input/output tokens
    /// and cache-token splits when emitted by the API. See
    /// <c>tests/.../message/assistant_with_usage.json</c> for the shape.
    /// </summary>
    public JsonElement? Usage { get; init; }
}
