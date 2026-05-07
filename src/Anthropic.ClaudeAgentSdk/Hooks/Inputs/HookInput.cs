using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>
/// Discriminated union of every hook input variant. Wire discriminator:
/// <c>hook_event_name</c> (snake_case property, PascalCase values — the
/// values match <see cref="HookEvent"/>). Mirrors
/// <c>claude_agent_sdk.types.HookInput</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "hook_event_name")]
[JsonDerivedType(typeof(PreToolUseHookInput), typeDiscriminator: "PreToolUse")]
[JsonDerivedType(typeof(PostToolUseHookInput), typeDiscriminator: "PostToolUse")]
[JsonDerivedType(typeof(PostToolUseFailureHookInput), typeDiscriminator: "PostToolUseFailure")]
[JsonDerivedType(typeof(UserPromptSubmitHookInput), typeDiscriminator: "UserPromptSubmit")]
[JsonDerivedType(typeof(StopHookInput), typeDiscriminator: "Stop")]
[JsonDerivedType(typeof(SubagentStopHookInput), typeDiscriminator: "SubagentStop")]
[JsonDerivedType(typeof(SubagentStartHookInput), typeDiscriminator: "SubagentStart")]
[JsonDerivedType(typeof(PreCompactHookInput), typeDiscriminator: "PreCompact")]
[JsonDerivedType(typeof(NotificationHookInput), typeDiscriminator: "Notification")]
[JsonDerivedType(typeof(PermissionRequestHookInput), typeDiscriminator: "PermissionRequest")]
public abstract record HookInput
{
    /// <summary>Identifier of the session the hook fired inside.</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>Path to the on-disk transcript file for the session.</summary>
    [JsonPropertyName("transcript_path")]
    public required string TranscriptPath { get; init; }

    /// <summary>Working directory when the hook fired.</summary>
    public required string Cwd { get; init; }

    /// <summary>Permission mode active for the session, when reported.</summary>
    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; init; }
}
