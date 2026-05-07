using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Sessions;

/// <summary>
/// Key argument to <c>ISessionStore.ListSubkeysAsync</c>. Identical in
/// shape to <see cref="SessionKey"/> but without a <c>subpath</c> field —
/// listing always operates on the main-transcript scope.
/// </summary>
public sealed record SessionListSubkeysKey
{
    /// <summary>Caller-defined scope (default: sanitised cwd).</summary>
    [JsonPropertyName("project_key")]
    public required string ProjectKey { get; init; }

    /// <summary>Stable session identifier.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}
