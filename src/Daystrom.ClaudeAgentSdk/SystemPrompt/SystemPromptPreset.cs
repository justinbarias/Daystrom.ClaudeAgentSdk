namespace Daystrom.ClaudeAgentSdk.SystemPrompt;

/// <summary>
/// System-prompt variant that opts into Claude Code's bundled preset
/// (<c>claude_code</c>), optionally with appended instructions.
/// Mirrors <c>claude_agent_sdk.types.SystemPromptPreset</c>.
/// </summary>
/// <param name="Append">
/// Optional text appended to the preset prompt. Emitted as
/// <c>--append-system-prompt &lt;value&gt;</c> alongside the preset selector.
/// </param>
/// <param name="ExcludeDynamicSections">
/// When true, strip per-user dynamic sections (working directory,
/// auto-memory, git status) from the system prompt so it stays static and
/// cacheable across users. The stripped content is re-injected into the
/// first user message so the model still sees it. Requires a Claude Code
/// CLI version that supports the option; older CLIs silently ignore it.
/// </param>
public sealed record SystemPromptPreset(string? Append = null, bool? ExcludeDynamicSections = null)
    : SystemPromptSpec;
