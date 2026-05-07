using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// One row in the breakdown carried by <see cref="ContextUsageResponse"/>.
/// </summary>
public sealed record ContextUsageCategory
{
    /// <summary>Display name (e.g. <c>system_prompt</c>, <c>tools</c>, <c>messages</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Token count attributed to this category.</summary>
    public required int Tokens { get; init; }

    /// <summary>Display color hint used by the CLI's UI.</summary>
    public required string Color { get; init; }

    /// <summary>Whether this category is currently deferred (lazy-loaded).</summary>
    [JsonPropertyName("isDeferred")]
    public bool? IsDeferred { get; init; }
}
