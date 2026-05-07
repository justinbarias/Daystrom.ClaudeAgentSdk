namespace Daystrom.ClaudeAgentSdk.Permissions;

/// <summary>Deny the tool call. <see cref="Message"/> is shown to the user.</summary>
public sealed record PermissionResultDeny : PermissionResult
{
    /// <summary>Reason shown to the user when the deny is surfaced.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>If true, the deny also interrupts the in-flight assistant turn.</summary>
    public bool Interrupt { get; init; }
}
