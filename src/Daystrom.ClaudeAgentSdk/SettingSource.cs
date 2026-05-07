using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk;

/// <summary>
/// Filesystem layer the CLI should load settings from.
/// Mirrors <c>claude_agent_sdk.types.SettingSource</c>.
/// </summary>
public enum SettingSource
{
    /// <summary>Global user settings (e.g. <c>~/.claude/settings.json</c>).</summary>
    [JsonStringEnumMemberName("user")]
    User,

    /// <summary>Project settings (e.g. <c>.claude/settings.json</c>).</summary>
    [JsonStringEnumMemberName("project")]
    Project,

    /// <summary>Local settings (e.g. <c>.claude/settings.local.json</c>).</summary>
    [JsonStringEnumMemberName("local")]
    Local,
}
