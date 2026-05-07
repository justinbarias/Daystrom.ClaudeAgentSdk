using System;

namespace Anthropic.ClaudeAgentSdk.Errors;

/// <summary>
/// Thrown when the Claude Code CLI subprocess exits with a non-zero status
/// or fails in a way the SDK cannot recover from. Carries the exit code and
/// the captured stderr (if any) for diagnosis.
/// </summary>
/// <remarks>
/// Mirrors <c>claude_agent_sdk._errors.ProcessError</c>.
/// </remarks>
public sealed class ProcessException : ClaudeSdkException
{
    /// <summary>Process exit code, or <c>null</c> if the process never exited.</summary>
    public int? ExitCode { get; }

    /// <summary>Captured stderr output, or <c>null</c> if no stderr was forwarded.</summary>
    public string? Stderr { get; }

    /// <summary>
    /// Initializes a new instance, appending the exit code and stderr to the
    /// formatted message in the same shape as the Python SDK.
    /// </summary>
    public ProcessException(string message, int? exitCode = null, string? stderr = null)
        : base(BuildMessage(message, exitCode, stderr))
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    /// <summary>
    /// Initializes a new instance with an inner exception for cases where the
    /// process failure was triggered by another fault (e.g. an I/O error).
    /// </summary>
    public ProcessException(
        string message,
        Exception? innerException,
        int? exitCode = null,
        string? stderr = null
    )
        : base(BuildMessage(message, exitCode, stderr), innerException)
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    private static string BuildMessage(string message, int? exitCode, string? stderr)
    {
        var result = message;
        if (exitCode is not null)
        {
            result = $"{result} (exit code: {exitCode})";
        }
        if (!string.IsNullOrEmpty(stderr))
        {
            result = $"{result}\nError output: {stderr}";
        }
        return result;
    }
}
