using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Sessions;

/// <summary>
/// Identifies a session transcript or sub-agent transcript inside an
/// <c>ISessionStore</c>. Main transcripts have no <see cref="Subpath"/>;
/// sub-agent transcripts include a <see cref="Subpath"/> like
/// <c>"subagents/agent-{id}"</c> that mirrors the on-disk layout.
/// </summary>
public sealed record SessionKey
{
    /// <summary>
    /// Caller-defined scope (default: sanitised cwd). Multi-tenant
    /// deployments should set this to a tenant id or project name.
    /// </summary>
    [JsonPropertyName("project_key")]
    public required string ProjectKey { get; init; }

    /// <summary>Stable session identifier.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Omit for the main transcript; set for sub-agent files. Empty string
    /// is invalid — omit the field for the main transcript.
    /// </summary>
    public string? Subpath { get; init; }
}
