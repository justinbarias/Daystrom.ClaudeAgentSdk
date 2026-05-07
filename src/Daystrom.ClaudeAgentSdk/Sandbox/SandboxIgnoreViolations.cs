using System.Collections.Generic;

namespace Daystrom.ClaudeAgentSdk.Sandbox;

/// <summary>
/// Sandbox violations to ignore — file or network paths the CLI should
/// not block even if they would otherwise trip a sandbox rule.
/// </summary>
/// <remarks>
/// Like <see cref="SandboxSettings"/>, no-op on native Windows.
/// </remarks>
public sealed record SandboxIgnoreViolations
{
    /// <summary>File paths whose access violations are ignored.</summary>
    public IReadOnlyList<string>? File { get; init; }

    /// <summary>Network hosts whose access violations are ignored.</summary>
    public IReadOnlyList<string>? Network { get; init; }
}
