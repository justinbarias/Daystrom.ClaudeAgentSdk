namespace Daystrom.ClaudeAgentSdk.SystemPrompt;

/// <summary>
/// System-prompt variant that loads the prompt body from a file on disk.
/// Emitted as <c>--system-prompt-file &lt;path&gt;</c> on the CLI command
/// line. Mirrors <c>claude_agent_sdk.types.SystemPromptFile</c>.
/// </summary>
public sealed record SystemPromptFile(string Path) : SystemPromptSpec;
