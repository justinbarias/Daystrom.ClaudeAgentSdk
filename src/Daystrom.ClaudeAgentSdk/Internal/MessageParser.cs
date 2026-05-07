using System.Text.Json;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Json;
using Daystrom.ClaudeAgentSdk.Messages;

namespace Daystrom.ClaudeAgentSdk.Internal;

/// <summary>
/// Translates a single NDJSON line (already parsed into a
/// <see cref="JsonElement"/> by <c>NdjsonReader</c>) into a
/// <see cref="Message"/> record. Mirrors
/// <c>claude_agent_sdk._internal.message_parser.parse_message</c>.
/// </summary>
/// <remarks>
/// <para>
/// Always routes through <see cref="ClaudeAgentJsonContext"/> so the call
/// site is AOT-safe (no reflection).
/// </para>
/// <para>
/// Forward-compat: unknown wire <c>type</c> values yield <see langword="null"/>
/// — they are not protocol errors, they are CLI versions newer than the
/// SDK. Mirrors the Python parser's silent-skip behaviour at
/// <c>_internal/message_parser.py:281</c>.
/// </para>
/// <para>
/// <see cref="SystemMessage"/> carries a second-level <c>subtype</c> field
/// that STJ's single-level polymorphism cannot dispatch on. After a base
/// <see cref="SystemMessage"/> is produced, the parser inspects its
/// <see cref="SystemMessage.Subtype"/> and re-deserialises into the
/// matching concrete record (<see cref="TaskStartedMessage"/>,
/// <see cref="TaskProgressMessage"/>, <see cref="TaskNotificationMessage"/>,
/// <see cref="MirrorErrorMessage"/>) when one is recognised. Unknown
/// subtypes pass through as bare <see cref="SystemMessage"/>.
/// </para>
/// </remarks>
internal static class MessageParser
{
    /// <summary>
    /// Parses a single wire object. Returns <see langword="null"/> when the
    /// payload is not an object, has no <c>type</c> string, or carries a
    /// type discriminator the SDK does not recognise.
    /// </summary>
    /// <exception cref="CliJsonDecodeException">
    /// Thrown when the payload's <c>type</c> is recognised but the rest of
    /// the schema fails to deserialise (missing required field, wrong
    /// shape, etc.). The raw JSON of the offending element is preserved on
    /// <see cref="CliJsonDecodeException.RawLine"/>.
    /// </exception>
    public static Message? Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (
            !element.TryGetProperty("type", out var typeProp)
            || typeProp.ValueKind != JsonValueKind.String
        )
        {
            return null;
        }

        var type = typeProp.GetString();
        if (
            type
            is not (
                "user"
                or "assistant"
                or "system"
                or "result"
                or "stream_event"
                or "rate_limit_event"
            )
        )
        {
            return null;
        }

        Message? message;
        try
        {
            message = element.Deserialize(ClaudeAgentJsonContext.Default.Message);
        }
        catch (JsonException ex)
        {
            throw new CliJsonDecodeException(element.GetRawText(), ex);
        }

        if (message is SystemMessage system && system.GetType() == typeof(SystemMessage))
        {
            try
            {
                return system.Subtype switch
                {
                    "task_started" => element.Deserialize(
                        ClaudeAgentJsonContext.Default.TaskStartedMessage
                    ),
                    "task_progress" => element.Deserialize(
                        ClaudeAgentJsonContext.Default.TaskProgressMessage
                    ),
                    "task_notification" => element.Deserialize(
                        ClaudeAgentJsonContext.Default.TaskNotificationMessage
                    ),
                    "mirror_error" => element.Deserialize(
                        ClaudeAgentJsonContext.Default.MirrorErrorMessage
                    ),
                    _ => system,
                };
            }
            catch (JsonException ex)
            {
                throw new CliJsonDecodeException(element.GetRawText(), ex);
            }
        }

        return message;
    }
}
