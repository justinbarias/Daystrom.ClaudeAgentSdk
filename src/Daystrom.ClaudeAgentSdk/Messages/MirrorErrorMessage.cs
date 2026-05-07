using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// System message emitted when an <c>ISessionStore.AppendAsync</c> call
/// fails. Non-fatal — the local-disk transcript is already durable and
/// the session continues; the mirrored copy in the external store will be
/// missing the failed batch. Sub-discriminator:
/// <c>subtype = "mirror_error"</c>.
/// </summary>
/// <remarks>
/// SDK-synthesised — never emitted by the CLI subprocess directly. See
/// Python <c>_internal/message_parser.py:204</c> for the parallel.
/// </remarks>
public sealed record MirrorErrorMessage : SystemMessage
{
    /// <summary>Session key whose append failed.</summary>
    [JsonPropertyName("key")]
    public Sessions.SessionKey? Key { get; init; }

    /// <summary>Underlying error message from the store adapter.</summary>
    public string Error { get; init; } = string.Empty;
}
