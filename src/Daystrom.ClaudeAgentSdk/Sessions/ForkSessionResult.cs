using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Sessions;

/// <summary>
/// Result of a session fork operation — the UUID of the newly forked
/// session. Mirrors <c>claude_agent_sdk._internal.session_mutations.ForkSessionResult</c>.
/// </summary>
public sealed record ForkSessionResult
{
    /// <summary>UUID of the new forked session.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}
