using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Terminal message marking the end of a query. Wire discriminator:
/// <c>result</c>. The byte-identical shape between one-shot
/// (<c>--print</c>) and streaming modes is what makes Phase 5's fast path
/// safe to reuse the same parser as the streaming client.
/// </summary>
public sealed record ResultMessage : Messages.Message
{
    /// <summary>Result subtype (e.g. <c>success</c>, <c>error_max_turns</c>, <c>error_max_budget_usd</c>).</summary>
    public required string Subtype { get; init; }

    /// <summary>Total wall-clock duration in milliseconds.</summary>
    [JsonPropertyName("duration_ms")]
    public required int DurationMs { get; init; }

    /// <summary>Time spent waiting on the API in milliseconds.</summary>
    [JsonPropertyName("duration_api_ms")]
    public required int DurationApiMs { get; init; }

    /// <summary>True when the query terminated due to an error.</summary>
    [JsonPropertyName("is_error")]
    public required bool IsError { get; init; }

    /// <summary>Number of conversation turns executed.</summary>
    [JsonPropertyName("num_turns")]
    public required int NumTurns { get; init; }

    /// <summary>Session identifier.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>API-side stop reason on the final assistant turn (<c>end_turn</c>, <c>max_tokens</c>, …).</summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    /// <summary>Total cost in US dollars when reported.</summary>
    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; init; }

    /// <summary>Aggregate API usage breakdown when present.</summary>
    public JsonElement? Usage { get; init; }

    /// <summary>Final assistant text when the query ran in <c>--print</c> mode.</summary>
    public string? Result { get; init; }

    /// <summary>Schema-validated structured output when an output schema was supplied.</summary>
    [JsonPropertyName("structured_output")]
    public JsonElement? StructuredOutput { get; init; }

    /// <summary>Per-model usage breakdown. Wire field is camelCase.</summary>
    [JsonPropertyName("modelUsage")]
    public JsonElement? ModelUsage { get; init; }

    /// <summary>Per-turn permission denials when any occurred.</summary>
    [JsonPropertyName("permission_denials")]
    public JsonElement? PermissionDenials { get; init; }

    /// <summary>Errors encountered during the query.</summary>
    public IReadOnlyList<string>? Errors { get; init; }

    /// <summary>SDK message identifier when set.</summary>
    public string? Uuid { get; init; }
}
