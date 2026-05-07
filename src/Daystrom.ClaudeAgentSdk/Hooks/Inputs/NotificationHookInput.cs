using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired when the CLI emits a user-visible notification.</summary>
public sealed record NotificationHookInput : HookInput
{
    /// <summary>Body of the notification.</summary>
    public required string Message { get; init; }

    /// <summary>Optional title of the notification.</summary>
    public string? Title { get; init; }

    /// <summary>Notification category as classified by the CLI.</summary>
    [JsonPropertyName("notification_type")]
    public required string NotificationType { get; init; }
}
