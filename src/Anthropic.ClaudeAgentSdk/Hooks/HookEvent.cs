using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Hooks;

/// <summary>
/// Lifecycle event a hook can subscribe to.
/// Mirrors <c>claude_agent_sdk.types.HookEvent</c>. Wire values are
/// PascalCase (matching the CLI's <c>hook_event_name</c> field), unlike
/// the rest of the wire format which is snake_case.
/// </summary>
public enum HookEvent
{
    /// <summary>Before a tool is invoked. Hook can deny, modify input, or pass.</summary>
    [JsonStringEnumMemberName("PreToolUse")]
    PreToolUse,

    /// <summary>After a tool returns successfully.</summary>
    [JsonStringEnumMemberName("PostToolUse")]
    PostToolUse,

    /// <summary>After a tool fails or is interrupted.</summary>
    [JsonStringEnumMemberName("PostToolUseFailure")]
    PostToolUseFailure,

    /// <summary>When a user prompt is submitted to the model.</summary>
    [JsonStringEnumMemberName("UserPromptSubmit")]
    UserPromptSubmit,

    /// <summary>When the main agent finishes a turn.</summary>
    [JsonStringEnumMemberName("Stop")]
    Stop,

    /// <summary>When a sub-agent finishes its task.</summary>
    [JsonStringEnumMemberName("SubagentStop")]
    SubagentStop,

    /// <summary>When a sub-agent is launched via the Task tool.</summary>
    [JsonStringEnumMemberName("SubagentStart")]
    SubagentStart,

    /// <summary>Before context compaction runs.</summary>
    [JsonStringEnumMemberName("PreCompact")]
    PreCompact,

    /// <summary>When the CLI emits a user-visible notification.</summary>
    [JsonStringEnumMemberName("Notification")]
    Notification,

    /// <summary>When a permission prompt is about to be raised to the user.</summary>
    [JsonStringEnumMemberName("PermissionRequest")]
    PermissionRequest,
}
