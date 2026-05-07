using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Response shape for <c>IClaudeAgentClient.GetContextUsageAsync</c>.
/// Provides a breakdown of current context-window usage by category,
/// matching the data shown by the CLI's <c>/context</c> command. Most
/// fields are camelCase on the wire — they carry explicit
/// <see cref="JsonPropertyNameAttribute"/> overrides.
/// </summary>
public sealed record ContextUsageResponse
{
    /// <summary>Token usage broken down by category.</summary>
    public required IReadOnlyList<ContextUsageCategory> Categories { get; init; }

    /// <summary>Total tokens currently in the context window.</summary>
    [JsonPropertyName("totalTokens")]
    public required int TotalTokens { get; init; }

    /// <summary>Effective maximum tokens (may be reduced by autocompact buffer).</summary>
    [JsonPropertyName("maxTokens")]
    public required int MaxTokens { get; init; }

    /// <summary>Raw model context-window size.</summary>
    [JsonPropertyName("rawMaxTokens")]
    public required int RawMaxTokens { get; init; }

    /// <summary>Percentage of context window used (0–100).</summary>
    public required double Percentage { get; init; }

    /// <summary>Model the usage is calculated for.</summary>
    public required string Model { get; init; }

    /// <summary>Whether autocompact is enabled for this session.</summary>
    [JsonPropertyName("isAutoCompactEnabled")]
    public required bool IsAutoCompactEnabled { get; init; }

    /// <summary>CLAUDE.md and memory files loaded.</summary>
    [JsonPropertyName("memoryFiles")]
    public required IReadOnlyList<JsonElement> MemoryFiles { get; init; }

    /// <summary>MCP tools breakdown.</summary>
    [JsonPropertyName("mcpTools")]
    public required IReadOnlyList<JsonElement> McpTools { get; init; }

    /// <summary>Agent definitions breakdown.</summary>
    public required IReadOnlyList<JsonElement> Agents { get; init; }

    /// <summary>Visual grid representation used by the CLI display.</summary>
    [JsonPropertyName("gridRows")]
    public required IReadOnlyList<IReadOnlyList<JsonElement>> GridRows { get; init; }

    /// <summary>Token threshold at which autocompact triggers.</summary>
    [JsonPropertyName("autoCompactThreshold")]
    public int? AutoCompactThreshold { get; init; }

    /// <summary>Built-in tools deferred from the initial tool list.</summary>
    [JsonPropertyName("deferredBuiltinTools")]
    public IReadOnlyList<JsonElement>? DeferredBuiltinTools { get; init; }

    /// <summary>System (built-in) tools.</summary>
    [JsonPropertyName("systemTools")]
    public IReadOnlyList<JsonElement>? SystemTools { get; init; }

    /// <summary>System prompt sections.</summary>
    [JsonPropertyName("systemPromptSections")]
    public IReadOnlyList<JsonElement>? SystemPromptSections { get; init; }

    /// <summary>Slash command usage summary.</summary>
    [JsonPropertyName("slashCommands")]
    public JsonElement? SlashCommands { get; init; }

    /// <summary>Skill usage summary.</summary>
    public JsonElement? Skills { get; init; }

    /// <summary>Detailed message-token breakdown.</summary>
    [JsonPropertyName("messageBreakdown")]
    public JsonElement? MessageBreakdown { get; init; }

    /// <summary>Cumulative API usage for the session.</summary>
    [JsonPropertyName("apiUsage")]
    public JsonElement? ApiUsage { get; init; }
}
