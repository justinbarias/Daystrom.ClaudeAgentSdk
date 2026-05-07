using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk;

/// <summary>
/// Controls how the CLI handles permission prompts during a session.
/// Mirrors <c>claude_agent_sdk.types.PermissionMode</c>.
/// </summary>
public enum PermissionMode
{
    /// <summary>Standard permission behavior; the CLI prompts for risky operations.</summary>
    [JsonStringEnumMemberName("default")]
    Default,

    /// <summary>Auto-accept file edit operations without prompting.</summary>
    [JsonStringEnumMemberName("acceptEdits")]
    AcceptEdits,

    /// <summary>Bypass all permission checks. Use with care.</summary>
    [JsonStringEnumMemberName("bypassPermissions")]
    BypassPermissions,

    /// <summary>Planning mode: the CLI plans without executing tools.</summary>
    [JsonStringEnumMemberName("plan")]
    Plan,

    /// <summary>Don't prompt for permissions; deny anything not pre-approved.</summary>
    [JsonStringEnumMemberName("dontAsk")]
    DontAsk,

    /// <summary>Auto-decide based on heuristics.</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,
}
