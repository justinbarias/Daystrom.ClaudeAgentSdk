using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Outputs;

/// <summary>
/// <see cref="HookSpecificOutput"/> for the
/// <see cref="HookEvent.PermissionRequest"/> event. Carries the decision
/// the hook wants the CLI to apply (allow / deny / ask, plus optional
/// updates) as a raw JSON element since the precise shape mirrors the
/// CLI's permission-decision schema.
/// </summary>
public sealed record PermissionRequestHookSpecificOutput : HookSpecificOutput
{
    /// <summary>Permission decision to apply.</summary>
    public required JsonElement Decision { get; init; }
}
