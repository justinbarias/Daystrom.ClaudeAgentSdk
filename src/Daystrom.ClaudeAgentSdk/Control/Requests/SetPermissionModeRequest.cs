namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Outbound <c>set_permission_mode</c> control request — switches the
/// active <see cref="PermissionMode"/> for the running session. Mirrors
/// the Python SDK at <c>_internal/query.py:686–693</c>.
/// </summary>
/// <remarks>
/// Wire shape: <c>{ "subtype": "set_permission_mode", "mode": "plan" }</c>.
/// <see cref="Mode"/> serialises via the enum's
/// <c>JsonStringEnumMemberName</c> attributes (e.g. <c>acceptEdits</c>,
/// <c>bypassPermissions</c>, <c>plan</c>). See
/// <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/set_permission_mode_request.json</c>.
/// </remarks>
internal sealed record SetPermissionModeRequest : ControlRequestPayload
{
    /// <summary>The permission mode to switch to.</summary>
    public required PermissionMode Mode { get; init; }
}
