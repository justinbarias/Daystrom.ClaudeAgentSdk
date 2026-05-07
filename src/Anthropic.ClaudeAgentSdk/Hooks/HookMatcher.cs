using System.Collections.Generic;

namespace Anthropic.ClaudeAgentSdk.Hooks;

/// <summary>
/// Configuration for one hook registration: a matcher pattern, the
/// callbacks to invoke, and an optional timeout. Mirrors
/// <c>claude_agent_sdk.types.HookMatcher</c>.
/// </summary>
/// <remarks>
/// See https://docs.anthropic.com/en/docs/claude-code/hooks#structure for
/// the matcher syntax. For PreToolUse/PostToolUse, the matcher is a tool
/// name (e.g. <c>Bash</c>) or pipe-joined pattern (<c>Write|MultiEdit|Edit</c>).
/// For event types without a tool dimension, leave <see cref="Matcher"/>
/// null.
/// </remarks>
public sealed record HookMatcher
{
    /// <summary>Pattern that selects which invocations the hook applies to. Null matches all.</summary>
    public string? Matcher { get; init; }

    /// <summary>Hook callbacks to invoke when the matcher fires.</summary>
    public IReadOnlyList<HookHandler> Hooks { get; init; } = [];

    /// <summary>Per-matcher timeout in seconds; null inherits the SDK default (60s).</summary>
    public double? Timeout { get; init; }
}
