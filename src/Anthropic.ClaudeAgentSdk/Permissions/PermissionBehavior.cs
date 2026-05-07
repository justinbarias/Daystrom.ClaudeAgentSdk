using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Permissions;

/// <summary>
/// Action a permission rule applies. Mirrors
/// <c>claude_agent_sdk.types.PermissionBehavior</c>.
/// </summary>
public enum PermissionBehavior
{
    /// <summary>Allow the action.</summary>
    [JsonStringEnumMemberName("allow")]
    Allow,

    /// <summary>Deny the action.</summary>
    [JsonStringEnumMemberName("deny")]
    Deny,

    /// <summary>Prompt the user.</summary>
    [JsonStringEnumMemberName("ask")]
    Ask,
}
