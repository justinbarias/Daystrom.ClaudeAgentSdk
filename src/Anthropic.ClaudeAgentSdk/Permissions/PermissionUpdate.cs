using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Permissions;

/// <summary>
/// Permission-rule update applied to the session. The <see cref="Type"/>
/// discriminator selects which fields are meaningful — <c>addRules</c>,
/// <c>replaceRules</c>, <c>removeRules</c> use <see cref="Rules"/> and
/// <see cref="Behavior"/>; <c>setMode</c> uses <see cref="Mode"/>;
/// <c>addDirectories</c> and <c>removeDirectories</c> use
/// <see cref="Directories"/>. Mirrors
/// <c>claude_agent_sdk.types.PermissionUpdate</c>.
/// </summary>
/// <remarks>
/// The Python type is a single dataclass with a conditional <c>to_dict</c>
/// rather than a polymorphic union. The .NET shape mirrors that — a flat
/// record with optional fields — because the discriminator gates *which*
/// fields are present, not which subtype is constructed. Wire values are
/// camelCase.
/// </remarks>
public sealed record PermissionUpdate
{
    /// <summary>
    /// Update kind: <c>addRules</c>, <c>replaceRules</c>, <c>removeRules</c>,
    /// <c>setMode</c>, <c>addDirectories</c>, or <c>removeDirectories</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>Rules payload for the rules-based variants.</summary>
    public IReadOnlyList<PermissionRuleValue>? Rules { get; init; }

    /// <summary>Behavior for the rules-based variants.</summary>
    public PermissionBehavior? Behavior { get; init; }

    /// <summary>Mode for the <c>setMode</c> variant.</summary>
    public PermissionMode? Mode { get; init; }

    /// <summary>Directories for the directory-based variants.</summary>
    public IReadOnlyList<string>? Directories { get; init; }

    /// <summary>Where to persist the update.</summary>
    public PermissionUpdateDestination? Destination { get; init; }
}
