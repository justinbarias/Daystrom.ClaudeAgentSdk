using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Outputs;

/// <summary>
/// Discriminated union for the <c>hookSpecificOutput</c> field of a sync
/// hook response. Wire discriminator: <c>hookEventName</c> (camelCase
/// property, PascalCase values — note the asymmetry with the input side
/// which uses snake_case <c>hook_event_name</c>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "hookEventName")]
[JsonDerivedType(typeof(NotificationHookSpecificOutput), typeDiscriminator: "Notification")]
[JsonDerivedType(typeof(SubagentStartHookSpecificOutput), typeDiscriminator: "SubagentStart")]
[JsonDerivedType(
    typeof(PermissionRequestHookSpecificOutput),
    typeDiscriminator: "PermissionRequest"
)]
[JsonDerivedType(
    typeof(PostToolUseFailureHookSpecificOutput),
    typeDiscriminator: "PostToolUseFailure"
)]
public abstract record HookSpecificOutput;
