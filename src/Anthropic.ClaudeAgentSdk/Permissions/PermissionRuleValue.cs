using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Permissions;

/// <summary>
/// One rule inside a <see cref="PermissionUpdate"/> with type
/// <c>addRules</c>, <c>replaceRules</c>, or <c>removeRules</c>. Wire field
/// names are camelCase (<c>toolName</c>, <c>ruleContent</c>) per Python's
/// <c>PermissionUpdate.to_dict</c>.
/// </summary>
public sealed record PermissionRuleValue
{
    /// <summary>Tool the rule applies to.</summary>
    [JsonPropertyName("toolName")]
    public required string ToolName { get; init; }

    /// <summary>Optional rule body (e.g. a glob).</summary>
    [JsonPropertyName("ruleContent")]
    public string? RuleContent { get; init; }
}
