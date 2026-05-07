using System;

namespace Daystrom.ClaudeAgentSdk.Errors;

/// <summary>
/// Thrown when no Claude Code CLI binary can be resolved through any of the
/// lookup strategies (explicit <c>CliPath</c>, bundled native package,
/// <c>PATH</c>, npm/local fallbacks). The message includes every path
/// attempted to make diagnosis trivial.
/// </summary>
/// <remarks>
/// Mirrors <c>claude_agent_sdk._errors.CLINotFoundError</c>.
/// </remarks>
public sealed class CliNotFoundException : CliConnectionException
{
    /// <summary>The CLI path the resolver was asked to use, if any.</summary>
    public string? CliPath { get; }

    /// <summary>Initializes a new instance with the default message.</summary>
    public CliNotFoundException()
        : base("Claude Code not found") { }

    /// <summary>Initializes a new instance with the specified message.</summary>
    public CliNotFoundException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance whose message includes the rejected
    /// <paramref name="cliPath"/> appended to <paramref name="message"/>.
    /// </summary>
    public CliNotFoundException(string message, string? cliPath)
        : base(cliPath is null ? message : $"{message}: {cliPath}")
    {
        CliPath = cliPath;
    }
}
