using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Daystrom.ClaudeAgentSdk.Agents;
using Daystrom.ClaudeAgentSdk.Agents.Plugins;
using Daystrom.ClaudeAgentSdk.Hooks;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.OutputFormat;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Sandbox;
using Daystrom.ClaudeAgentSdk.Sessions;
using Daystrom.ClaudeAgentSdk.SystemPrompt;
using ThinkingConfigType = Daystrom.ClaudeAgentSdk.ThinkingConfig.ThinkingConfig;

namespace Daystrom.ClaudeAgentSdk;

/// <summary>
/// Immutable, builder-friendly bag of options for a Claude Agent SDK
/// session. Mirrors <c>claude_agent_sdk.types.ClaudeAgentOptions</c>.
/// </summary>
/// <remarks>
/// <para>
/// Construct via the <see langword="with"/> expression on a default
/// instance, or via <see cref="ClaudeAgentOptions.Create"/> for a fluent
/// builder. The internal <c>CommandBuilder</c> turns these into the CLI's
/// argv in Phase 4.6; the wire flag emission lives there, not here.
/// </para>
/// <para>
/// Defaults match the Python SDK: empty lists for collection-valued
/// options, <c>null</c> for "leave the CLI's default in place" optional
/// scalars, and <c>false</c> for boolean toggles. Reference types use
/// <see cref="ImmutableDictionary{TKey,TValue}.Empty"/> /
/// <see cref="Array.Empty{T}"/> so unmodified options never allocate.
/// </para>
/// </remarks>
public sealed record ClaudeAgentOptions
{
    /// <summary>Claude model id or alias (<c>sonnet</c>, <c>opus</c>, <c>haiku</c>).</summary>
    public string? Model { get; init; }

    /// <summary>Fallback model used when the primary model is unavailable.</summary>
    public string? FallbackModel { get; init; }

    /// <summary>
    /// Tools the model may call without prompting for permission. To restrict
    /// which tools exist at all, use <see cref="Tools"/>.
    /// </summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Tools removed from the model's tool registry. Stronger than
    /// <see cref="AllowedTools"/> — disallowed tools cannot be used at all.
    /// </summary>
    public IReadOnlyList<string> DisallowedTools { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Base set of available built-in tools. <c>null</c> uses the CLI default;
    /// an empty list disables all built-in tools.
    /// </summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>
    /// Skills to enable for the main session. <c>null</c> applies the CLI's
    /// own defaults (which may auto-enable some skills); an empty list
    /// suppresses every skill from the listing; otherwise lists the skill
    /// names to enable. The SDK injects the <c>Skill</c> tool and adjusts
    /// <see cref="SettingSources"/> automatically when this is non-null.
    /// </summary>
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>Permission mode the session runs under.</summary>
    public PermissionMode? PermissionMode { get; init; }

    /// <summary>MCP tool name to delegate permission prompts to (optional).</summary>
    public string? PermissionPromptToolName { get; init; }

    /// <summary>
    /// Optional in-process callback invoked before each tool runs.
    /// Setting this forces the streaming control-protocol path.
    /// </summary>
    public CanUseToolDelegate? CanUseTool { get; init; }

    /// <summary>
    /// Hook callbacks keyed by lifecycle event. Setting any hook forces
    /// the streaming control-protocol path.
    /// </summary>
    public IReadOnlyDictionary<HookEvent, IReadOnlyList<HookMatcher>>? Hooks { get; init; }

    /// <summary>
    /// MCP server configurations keyed by server name. Including any
    /// <see cref="McpSdkServerConfig"/> forces the streaming control-protocol
    /// path so in-process tool routing can run.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerConfig>? McpServers { get; init; }

    /// <summary>Plugins to load for the session.</summary>
    public IReadOnlyList<SdkPluginConfig> Plugins { get; init; } = Array.Empty<SdkPluginConfig>();

    /// <summary>Programmatically-defined sub-agents keyed by agent name.</summary>
    public IReadOnlyDictionary<string, AgentDefinition>? Agents { get; init; }

    /// <summary>Extended-thinking configuration. Takes precedence over <see cref="MaxThinkingTokens"/>.</summary>
    public ThinkingConfigType? Thinking { get; init; }

    /// <summary>
    /// Legacy thinking-budget knob superseded by <see cref="Thinking"/>.
    /// On modern models this acts as on/off (0 disables; any other value
    /// enables adaptive thinking). Prefer
    /// <see cref="ThinkingConfig.ThinkingConfigAdaptive"/> /
    /// <see cref="ThinkingConfig.ThinkingConfigEnabled"/> on
    /// <see cref="Thinking"/>.
    /// </summary>
    public int? MaxThinkingTokens { get; init; }

    /// <summary>Effort hint (<c>low</c>, <c>medium</c>, <c>high</c>, <c>max</c>).</summary>
    public string? Effort { get; init; }

    /// <summary>Maximum number of conversation turns before the query stops.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>Maximum cost in USD before the query halts with an error result.</summary>
    public decimal? MaxBudgetUsd { get; init; }

    /// <summary>API-side task budget in tokens (sent with the task-budgets beta header).</summary>
    public TaskBudget? TaskBudget { get; init; }

    /// <summary>Session id to resume. Mutually exclusive with <see cref="ContinueConversation"/> unless <see cref="ForkSession"/> is also set.</summary>
    public string? Resume { get; init; }

    /// <summary>Continue the most recent conversation in the cwd.</summary>
    public bool ContinueConversation { get; init; }

    /// <summary>Caller-supplied session id. Must be a valid UUID when set.</summary>
    public string? SessionId { get; init; }

    /// <summary>When resuming, fork to a new session id rather than continuing the previous one.</summary>
    public bool ForkSession { get; init; }

    /// <summary>External transcript-mirror adapter; see <see cref="ISessionStore"/>.</summary>
    public ISessionStore? SessionStore { get; init; }

    /// <summary>Emit partial assistant-message events while streaming.</summary>
    public bool IncludePartialMessages { get; init; }

    /// <summary>Path to an additional settings JSON file (CLI <c>--settings</c>).</summary>
    public string? Settings { get; init; }

    /// <summary>
    /// Sandbox settings for command isolation. <b>No-op on native Windows</b>
    /// — see <see cref="SandboxSettings"/> for caveats.
    /// </summary>
    public SandboxSettings? Sandbox { get; init; }

    /// <summary>System-prompt configuration.</summary>
    public SystemPromptSpec? SystemPrompt { get; init; }

    /// <summary>
    /// Filesystem layers to load CLI settings from. <c>null</c> matches the
    /// CLI default (all sources); an empty list disables filesystem
    /// settings entirely (SDK isolation mode); otherwise selects layers
    /// explicitly. Must include <see cref="SettingSource.Project"/> to
    /// load <c>CLAUDE.md</c> files.
    /// </summary>
    public IReadOnlyList<SettingSource>? SettingSources { get; init; }

    /// <summary>Additional directories the CLI can access beyond <see cref="Cwd"/>.</summary>
    public IReadOnlyList<string> AddDirs { get; init; } = Array.Empty<string>();

    /// <summary>Environment variables passed to the subprocess.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>Working directory for the subprocess. Defaults to the host process's cwd.</summary>
    public string? Cwd { get; init; }

    /// <summary>Optional user identifier associated with the session.</summary>
    public string? User { get; init; }

    /// <summary>
    /// Path to the Claude Code CLI executable. When set, takes precedence
    /// over the bundled binary and PATH lookup.
    /// </summary>
    public string? CliPath { get; init; }

    /// <summary>
    /// Callback invoked for each line written to the subprocess's stderr.
    /// When <c>null</c>, stderr is left at its default (inherited).
    /// </summary>
    public Action<string>? Stderr { get; init; }

    /// <summary>Anthropic API beta features to enable for the session.</summary>
    public IReadOnlyList<SdkBeta> Betas { get; init; } = Array.Empty<SdkBeta>();

    /// <summary>Structured-output configuration.</summary>
    public OutputFormatSpec? OutputFormat { get; init; }

    /// <summary>
    /// Additional CLI arguments. Keys are the flag name without leading
    /// <c>--</c>; values are the argument string, or <c>null</c> for boolean
    /// flags.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ExtraArgs { get; init; } =
        ImmutableDictionary<string, string?>.Empty;

    /// <summary>
    /// Maximum bytes the NDJSON reader will buffer for a single message
    /// before raising <see cref="Errors.CliJsonDecodeException"/>.
    /// <c>null</c> uses the SDK default of 1 MiB.
    /// </summary>
    public int? MaxBufferSize { get; init; }

    /// <summary>Enable file checkpointing so files can be rewound between turns.</summary>
    public bool EnableFileCheckpointing { get; init; }

    /// <summary>Returns a fresh <see cref="ClaudeAgentOptionsBuilder"/>.</summary>
    public static ClaudeAgentOptionsBuilder Create() => new();
}
