namespace Daystrom.ClaudeAgentSdk.SystemPrompt;

/// <summary>
/// System-prompt variant carrying an inline string. Emitted as
/// <c>--system-prompt &lt;value&gt;</c> on the CLI command line.
/// </summary>
public sealed record SystemPromptText(string Value) : SystemPromptSpec;
