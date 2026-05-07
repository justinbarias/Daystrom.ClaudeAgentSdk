using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Outputs;

/// <summary>
/// <see cref="HookSpecificOutput"/> for the <see cref="HookEvent.Notification"/> event.
/// </summary>
public sealed record NotificationHookSpecificOutput : HookSpecificOutput
{
    /// <summary>Extra context to add to the notification.</summary>
    [JsonPropertyName("additionalContext")]
    public string? AdditionalContext { get; init; }
}
