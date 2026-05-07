using System;

namespace Daystrom.ClaudeAgentSdk.Errors;

/// <summary>
/// Base exception for all errors thrown by the Claude Agent SDK. Catch this
/// to handle any SDK failure generically; catch one of the derived types
/// (<see cref="CliConnectionException"/>, <see cref="ProcessException"/>,
/// <see cref="CliJsonDecodeException"/>) for finer control.
/// </summary>
/// <remarks>
/// Mirrors <c>claude_agent_sdk._errors.ClaudeSDKError</c> in the Python SDK.
/// </remarks>
public class ClaudeSdkException : Exception
{
    /// <summary>Initializes a new instance with no message.</summary>
    public ClaudeSdkException() { }

    /// <summary>Initializes a new instance with the specified message.</summary>
    public ClaudeSdkException(string? message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    public ClaudeSdkException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
