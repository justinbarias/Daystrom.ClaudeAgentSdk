using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Result of a server-side tool call. <see cref="Content"/> is opaque to
/// this layer — its shape depends on the specific server tool (advisor
/// emits <c>advisor_result</c>/<c>advisor_redacted_result</c>/
/// <c>advisor_tool_result_error</c>; other server tools use different
/// shapes). Inspect <c>content["type"]</c> at the call site to dispatch.
/// </summary>
/// <remarks>
/// Wire discriminator: <c>advisor_tool_result</c> (NOT
/// <c>server_tool_result</c>) — the CLI emits the advisor-flavored name
/// even for non-advisor server tools. Source: Python SDK
/// <c>_internal/message_parser.py:140</c>.
/// </remarks>
public sealed record ServerToolResultBlock : ContentBlock
{
    /// <summary>The id of the matching <see cref="ServerToolUseBlock"/>.</summary>
    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    /// <summary>Raw tool-specific result payload.</summary>
    public required JsonElement Content { get; init; }
}
