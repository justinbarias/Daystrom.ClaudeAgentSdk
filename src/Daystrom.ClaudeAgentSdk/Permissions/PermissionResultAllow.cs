using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Permissions;

/// <summary>
/// Allow the tool call. Optionally rewrite the tool's input via
/// <see cref="UpdatedInput"/> or apply additional <see cref="UpdatedPermissions"/>.
/// </summary>
public sealed record PermissionResultAllow : PermissionResult
{
    /// <summary>Replacement tool input. Null leaves the original input unchanged.</summary>
    [JsonPropertyName("updated_input")]
    public JsonElement? UpdatedInput { get; init; }

    /// <summary>Permission updates to apply alongside the allow decision.</summary>
    [JsonPropertyName("updated_permissions")]
    public IReadOnlyList<PermissionUpdate>? UpdatedPermissions { get; init; }
}
