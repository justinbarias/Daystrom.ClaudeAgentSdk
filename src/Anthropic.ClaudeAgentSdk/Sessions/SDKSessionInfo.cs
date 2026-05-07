using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Sessions;

/// <summary>
/// Session metadata returned by the SDK's session-listing API. Contains
/// only data extractable from <c>stat</c> + head/tail reads — no full
/// JSONL parsing required. Mirrors <c>claude_agent_sdk.types.SDKSessionInfo</c>.
/// </summary>
public sealed record SDKSessionInfo
{
    /// <summary>Unique session identifier (UUID).</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Display title for the session — custom title, auto-generated
    /// summary, or first prompt.
    /// </summary>
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    /// <summary>Last-modified time in Unix epoch milliseconds.</summary>
    [JsonPropertyName("last_modified")]
    public required long LastModified { get; init; }

    /// <summary>
    /// Session file size in bytes. Only populated for local JSONL storage;
    /// may be <c>null</c> for remote storage backends.
    /// </summary>
    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }

    /// <summary>User-set custom title or AI-generated title.</summary>
    [JsonPropertyName("custom_title")]
    public string? CustomTitle { get; init; }

    /// <summary>First meaningful user prompt in the session.</summary>
    [JsonPropertyName("first_prompt")]
    public string? FirstPrompt { get; init; }

    /// <summary>Git branch at the end of the session.</summary>
    [JsonPropertyName("git_branch")]
    public string? GitBranch { get; init; }

    /// <summary>Working directory for the session.</summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>User-set session tag.</summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

    /// <summary>
    /// Creation time in Unix epoch milliseconds, extracted from the first
    /// entry's ISO timestamp field. More reliable than <c>stat().birthtime</c>
    /// which is unsupported on some filesystems.
    /// </summary>
    [JsonPropertyName("created_at")]
    public long? CreatedAt { get; init; }
}
