using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Daystrom.ClaudeAgentSdk;

/// <summary>
/// Wire-shape for a streaming user turn. Mirrors the dictionary the Python
/// SDK writes to the CLI's stdin
/// (<c>_internal/client.py:206-215</c>):
/// <code>
/// {
///   "type": "user",
///   "session_id": "&lt;SessionId&gt;",
///   "message": { "role": "user", "content": "&lt;Text&gt;" },
///   "parent_tool_use_id": &lt;ParentToolUseId-or-null&gt;
/// }
/// </code>
/// </summary>
/// <remarks>
/// The trailing newline that delimits NDJSON frames is the SDK's
/// responsibility — <see cref="ToWireJson"/> emits a single JSON document
/// without a newline; <see cref="ClaudeAgentClient"/> appends one before
/// writing.
/// </remarks>
public sealed record UserMessageInput
{
    /// <summary>User-visible text content of the turn. Required.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// Session id sent on the wire. Defaults to the empty string, which
    /// matches the Python SDK's "string-prompt fast path" (it writes
    /// <c>"session_id": ""</c> for plain string prompts and
    /// <c>"default"</c> when a follow-up turn is sent via
    /// <c>ClaudeSDKClient.query()</c>; the .NET client always defers
    /// session-id assignment to the CLI when this is empty).
    /// </summary>
    public string SessionId { get; init; } = "";

    /// <summary>
    /// Parent tool-use id when this user turn is a tool-result delivered
    /// by the SDK on behalf of the harness. <see langword="null"/> for
    /// top-level user turns.
    /// </summary>
    public string? ParentToolUseId { get; init; }

    /// <summary>Convenience constructor: build a turn from plain text.</summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public UserMessageInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>
    /// Required for record positional-style construction; callers should
    /// prefer <see cref="UserMessageInput(string)"/> or the object-init
    /// syntax with <see cref="Text"/>.
    /// </summary>
    public UserMessageInput() { }

    /// <summary>
    /// Serialises this turn into the exact NDJSON envelope the CLI expects.
    /// The returned string is a single JSON document with no trailing
    /// newline — the caller appends <c>"\n"</c> before writing.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Utf8JsonWriter"/> directly (no reflection, no
    /// runtime <see cref="JsonSerializerOptions"/>), so the call site is
    /// AOT-safe.
    /// </remarks>
    public string ToWireJson()
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "user");
            writer.WriteString("session_id", SessionId);
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteString("content", Text);
            writer.WriteEndObject();
            if (ParentToolUseId is null)
            {
                writer.WriteNull("parent_tool_use_id");
            }
            else
            {
                writer.WriteString("parent_tool_use_id", ParentToolUseId);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
