using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Permissions;

/// <summary>
/// Where a <c>PermissionUpdate</c> should be persisted.
/// Mirrors <c>claude_agent_sdk.types.PermissionUpdateDestination</c>.
/// </summary>
public enum PermissionUpdateDestination
{
    /// <summary>Apply to user-level settings.</summary>
    [JsonStringEnumMemberName("userSettings")]
    UserSettings,

    /// <summary>Apply to project-level settings.</summary>
    [JsonStringEnumMemberName("projectSettings")]
    ProjectSettings,

    /// <summary>Apply to local settings.</summary>
    [JsonStringEnumMemberName("localSettings")]
    LocalSettings,

    /// <summary>Apply only to the current session (not persisted).</summary>
    [JsonStringEnumMemberName("session")]
    Session,
}
