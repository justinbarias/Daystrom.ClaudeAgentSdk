using System;
using System.Text.Json;

namespace Anthropic.ClaudeAgentSdk.Errors;

/// <summary>
/// Thrown when the SDK fails to decode a line of NDJSON received from the
/// Claude Code CLI subprocess. The exception preserves the exact raw line
/// that triggered the failure so callers can diagnose protocol drift.
/// </summary>
/// <remarks>
/// Mirrors <c>claude_agent_sdk._errors.CLIJSONDecodeError</c>. Wraps the
/// underlying <see cref="JsonException"/> as <see cref="Exception.InnerException"/>.
/// </remarks>
public sealed class CliJsonDecodeException : ClaudeSdkException
{
    /// <summary>The raw NDJSON line that failed to decode.</summary>
    public string RawLine { get; }

    /// <summary>
    /// Initializes a new instance carrying the offending raw line and the
    /// underlying parser exception.
    /// </summary>
    public CliJsonDecodeException(string rawLine, Exception originalError)
        : base(BuildMessage(rawLine), originalError)
    {
        RawLine = rawLine;
    }

    private static string BuildMessage(string rawLine)
    {
        var preview = rawLine.Length > 100 ? rawLine[..100] : rawLine;
        return $"Failed to decode JSON: {preview}...";
    }
}
