using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Result of a tool call. <see cref="Content"/> is opaque (the CLI passes
/// through whatever the tool returned — a string, a list of message-style
/// dicts, or null) and is therefore typed as <see cref="JsonElement"/>.
/// Wire discriminator: <c>tool_result</c>.
/// </summary>
public sealed record ToolResultBlock : ContentBlock
{
    /// <summary>The id of the matching <see cref="ToolUseBlock"/>.</summary>
    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    /// <summary>Tool output. Null when the tool returned no content.</summary>
    public JsonElement? Content { get; init; }

    /// <summary>True when the tool result represents an error.</summary>
    [JsonPropertyName("is_error")]
    public bool? IsError { get; init; }
}
