using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Sandbox;

/// <summary>
/// Sandbox settings for command execution isolation. Mirrors
/// <c>claude_agent_sdk.types.SandboxSettings</c>. Wire fields are
/// camelCase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Platform note: native Windows hosts do not sandbox.</b> The CLI
/// implements sandboxing only on macOS, Linux, and WSL2. Setting any
/// field on this record on a native Windows host is a no-op — the SDK
/// still serialises the values into the CLI <c>--settings</c> payload,
/// but the CLI ignores them. <c>CliBinaryResolver</c> in Phase 4 logs an
/// <c>ILogger.LogWarning</c> once per process start when a non-null
/// sandbox is configured on native Windows.
/// </para>
/// <para>
/// Filesystem-read and filesystem-write restrictions are configured via
/// <see cref="Permissions.PermissionUpdate"/> rules (Read deny / Edit
/// allow), not via this type. Network restrictions go through
/// <see cref="SandboxNetworkConfig"/>; WebFetch allow/deny via
/// permission rules.
/// </para>
/// </remarks>
public sealed record SandboxSettings
{
    /// <summary>Enable bash sandboxing (macOS / Linux only). Default: false.</summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// When true, bash commands are auto-approved if they would run sandboxed.
    /// Default: true.
    /// </summary>
    [JsonPropertyName("autoAllowBashIfSandboxed")]
    public bool? AutoAllowBashIfSandboxed { get; init; }

    /// <summary>Commands that should run outside the sandbox (e.g. <c>git</c>, <c>docker</c>).</summary>
    [JsonPropertyName("excludedCommands")]
    public IReadOnlyList<string>? ExcludedCommands { get; init; }

    /// <summary>
    /// When false, all commands must run sandboxed (or be in
    /// <see cref="ExcludedCommands"/>). Default: true.
    /// </summary>
    [JsonPropertyName("allowUnsandboxedCommands")]
    public bool? AllowUnsandboxedCommands { get; init; }

    /// <summary>Network configuration for the sandbox.</summary>
    public SandboxNetworkConfig? Network { get; init; }

    /// <summary>Violations the sandbox should silently ignore.</summary>
    [JsonPropertyName("ignoreViolations")]
    public SandboxIgnoreViolations? IgnoreViolations { get; init; }

    /// <summary>
    /// Enable a weaker sandbox for unprivileged Docker environments
    /// (Linux only). Reduces security. Default: false.
    /// </summary>
    [JsonPropertyName("enableWeakerNestedSandbox")]
    public bool? EnableWeakerNestedSandbox { get; init; }
}
