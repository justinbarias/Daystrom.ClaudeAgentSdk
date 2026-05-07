using System;

namespace Anthropic.ClaudeAgentSdk.Errors;

/// <summary>
/// Thrown when the SDK cannot establish or maintain its stdio connection
/// to the Claude Code CLI subprocess.
/// </summary>
/// <remarks>
/// Mirrors <c>claude_agent_sdk._errors.CLIConnectionError</c>.
/// <see cref="CliNotFoundException"/> is a more specific subtype thrown when
/// no CLI binary can be resolved at all.
/// </remarks>
public class CliConnectionException : ClaudeSdkException
{
    /// <summary>Initializes a new instance with no message.</summary>
    public CliConnectionException() { }

    /// <summary>Initializes a new instance with the specified message.</summary>
    public CliConnectionException(string? message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    public CliConnectionException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
