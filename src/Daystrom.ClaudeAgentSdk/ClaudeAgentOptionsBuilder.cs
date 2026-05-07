using System;
using System.Collections.Generic;
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
/// Fluent builder for <see cref="ClaudeAgentOptions"/>. All setters return
/// <c>this</c>; <see cref="Build"/> snapshots the current state into an
/// immutable record.
/// </summary>
/// <remarks>
/// Builders are not thread-safe. Build a fresh instance per session, or
/// guard one shared builder with external synchronisation. The output of
/// <see cref="Build"/> is a value-equal record and is safe to share.
/// </remarks>
public sealed class ClaudeAgentOptionsBuilder
{
    private string? _model;
    private string? _fallbackModel;
    private List<string>? _allowedTools;
    private List<string>? _disallowedTools;
    private List<string>? _tools;
    private List<string>? _skills;
    private bool _skillsExplicitlyNull = true;
    private PermissionMode? _permissionMode;
    private string? _permissionPromptToolName;
    private CanUseToolDelegate? _canUseTool;
    private Dictionary<HookEvent, List<HookMatcher>>? _hooks;
    private Dictionary<string, McpServerConfig>? _mcpServers;
    private List<SdkPluginConfig>? _plugins;
    private Dictionary<string, AgentDefinition>? _agents;
    private ThinkingConfigType? _thinking;
    private int? _maxThinkingTokens;
    private string? _effort;
    private int? _maxTurns;
    private decimal? _maxBudgetUsd;
    private TaskBudget? _taskBudget;
    private string? _resume;
    private bool _continueConversation;
    private string? _sessionId;
    private bool _forkSession;
    private ISessionStore? _sessionStore;
    private bool _includePartialMessages;
    private string? _settings;
    private SandboxSettings? _sandbox;
    private SystemPromptSpec? _systemPrompt;
    private List<SettingSource>? _settingSources;
    private bool _settingSourcesExplicitlyNull = true;
    private List<string>? _addDirs;
    private Dictionary<string, string>? _env;
    private string? _cwd;
    private string? _user;
    private string? _cliPath;
    private Action<string>? _stderr;
    private List<SdkBeta>? _betas;
    private OutputFormatSpec? _outputFormat;
    private Dictionary<string, string?>? _extraArgs;
    private int? _maxBufferSize;
    private bool _enableFileCheckpointing;

    /// <summary>Set <see cref="ClaudeAgentOptions.Model"/>.</summary>
    public ClaudeAgentOptionsBuilder WithModel(string? model)
    {
        _model = model;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.FallbackModel"/>.</summary>
    public ClaudeAgentOptionsBuilder WithFallbackModel(string? model)
    {
        _fallbackModel = model;
        return this;
    }

    /// <summary>Append to <see cref="ClaudeAgentOptions.AllowedTools"/>.</summary>
    public ClaudeAgentOptionsBuilder WithAllowedTools(params string[] tools)
    {
        (_allowedTools ??= new List<string>()).AddRange(tools);
        return this;
    }

    /// <summary>Append to <see cref="ClaudeAgentOptions.DisallowedTools"/>.</summary>
    public ClaudeAgentOptionsBuilder WithDisallowedTools(params string[] tools)
    {
        (_disallowedTools ??= new List<string>()).AddRange(tools);
        return this;
    }

    /// <summary>
    /// Set <see cref="ClaudeAgentOptions.Tools"/> (the base tool registry).
    /// Pass an empty array to disable all built-in tools.
    /// </summary>
    public ClaudeAgentOptionsBuilder WithTools(params string[] tools)
    {
        _tools = new List<string>(tools);
        return this;
    }

    /// <summary>
    /// Set <see cref="ClaudeAgentOptions.Skills"/>. Pass an empty array to
    /// suppress all skills.
    /// </summary>
    public ClaudeAgentOptionsBuilder WithSkills(params string[] skills)
    {
        _skills = new List<string>(skills);
        _skillsExplicitlyNull = false;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.PermissionMode"/>.</summary>
    public ClaudeAgentOptionsBuilder WithPermissionMode(PermissionMode mode)
    {
        _permissionMode = mode;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.PermissionPromptToolName"/>.</summary>
    public ClaudeAgentOptionsBuilder WithPermissionPromptToolName(string name)
    {
        _permissionPromptToolName = name;
        return this;
    }

    /// <summary>
    /// Register the in-process permission callback. Setting this forces the
    /// streaming control-protocol path.
    /// </summary>
    public ClaudeAgentOptionsBuilder OnCanUseTool(CanUseToolDelegate handler)
    {
        _canUseTool = handler;
        return this;
    }

    /// <summary>
    /// Register one or more hook matchers for a lifecycle event. Multiple
    /// calls accumulate.
    /// </summary>
    public ClaudeAgentOptionsBuilder OnHook(HookEvent @event, params HookMatcher[] matchers)
    {
        _hooks ??= new Dictionary<HookEvent, List<HookMatcher>>();
        if (!_hooks.TryGetValue(@event, out var list))
        {
            list = new List<HookMatcher>();
            _hooks[@event] = list;
        }
        list.AddRange(matchers);
        return this;
    }

    /// <summary>Add an MCP server to <see cref="ClaudeAgentOptions.McpServers"/>.</summary>
    public ClaudeAgentOptionsBuilder WithMcpServer(string name, McpServerConfig config)
    {
        (_mcpServers ??= new Dictionary<string, McpServerConfig>())[name] = config;
        return this;
    }

    /// <summary>Append a plugin configuration.</summary>
    public ClaudeAgentOptionsBuilder WithPlugin(SdkPluginConfig plugin)
    {
        (_plugins ??= new List<SdkPluginConfig>()).Add(plugin);
        return this;
    }

    /// <summary>Register a programmatic sub-agent.</summary>
    public ClaudeAgentOptionsBuilder WithAgent(string name, AgentDefinition agent)
    {
        (_agents ??= new Dictionary<string, AgentDefinition>())[name] = agent;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.Thinking"/>.</summary>
    public ClaudeAgentOptionsBuilder WithThinking(ThinkingConfigType thinking)
    {
        _thinking = thinking;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.MaxThinkingTokens"/> (legacy).</summary>
    public ClaudeAgentOptionsBuilder WithMaxThinkingTokens(int tokens)
    {
        _maxThinkingTokens = tokens;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.Effort"/>.</summary>
    public ClaudeAgentOptionsBuilder WithEffort(string effort)
    {
        _effort = effort;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.MaxTurns"/>.</summary>
    public ClaudeAgentOptionsBuilder WithMaxTurns(int turns)
    {
        _maxTurns = turns;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.MaxBudgetUsd"/>.</summary>
    public ClaudeAgentOptionsBuilder WithMaxBudgetUsd(decimal usd)
    {
        _maxBudgetUsd = usd;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.TaskBudget"/>.</summary>
    public ClaudeAgentOptionsBuilder WithTaskBudget(TaskBudget budget)
    {
        _taskBudget = budget;
        return this;
    }

    /// <summary>Resume an existing session by id.</summary>
    public ClaudeAgentOptionsBuilder ResumeSession(string sessionId)
    {
        _resume = sessionId;
        return this;
    }

    /// <summary>Continue the most recent conversation in the cwd.</summary>
    public ClaudeAgentOptionsBuilder ContinueConversation()
    {
        _continueConversation = true;
        return this;
    }

    /// <summary>Pin the session id (must be a valid UUID).</summary>
    public ClaudeAgentOptionsBuilder WithSessionId(string sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    /// <summary>Fork the resumed session to a new id rather than continuing it.</summary>
    public ClaudeAgentOptionsBuilder ForkSession()
    {
        _forkSession = true;
        return this;
    }

    /// <summary>Mirror transcripts to the supplied <see cref="ISessionStore"/>.</summary>
    public ClaudeAgentOptionsBuilder WithSessionStore(ISessionStore store)
    {
        _sessionStore = store;
        return this;
    }

    /// <summary>Include partial assistant-message events while streaming.</summary>
    public ClaudeAgentOptionsBuilder IncludePartialMessages()
    {
        _includePartialMessages = true;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.Settings"/> (path to a settings JSON file).</summary>
    public ClaudeAgentOptionsBuilder WithSettings(string path)
    {
        _settings = path;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.Sandbox"/>.</summary>
    public ClaudeAgentOptionsBuilder WithSandbox(SandboxSettings sandbox)
    {
        _sandbox = sandbox;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.SystemPrompt"/> from a typed spec.</summary>
    public ClaudeAgentOptionsBuilder WithSystemPrompt(SystemPromptSpec spec)
    {
        _systemPrompt = spec;
        return this;
    }

    /// <summary>Convenience: set <see cref="ClaudeAgentOptions.SystemPrompt"/> from plain text.</summary>
    public ClaudeAgentOptionsBuilder WithSystemPrompt(string text) =>
        WithSystemPrompt(new SystemPromptText(text));

    /// <summary>
    /// Set <see cref="ClaudeAgentOptions.SettingSources"/>. Pass an empty
    /// array to disable filesystem settings entirely.
    /// </summary>
    public ClaudeAgentOptionsBuilder WithSettingSources(params SettingSource[] sources)
    {
        _settingSources = new List<SettingSource>(sources);
        _settingSourcesExplicitlyNull = false;
        return this;
    }

    /// <summary>Append directories to <see cref="ClaudeAgentOptions.AddDirs"/>.</summary>
    public ClaudeAgentOptionsBuilder WithAddDirs(params string[] dirs)
    {
        (_addDirs ??= new List<string>()).AddRange(dirs);
        return this;
    }

    /// <summary>Set a single environment variable on the subprocess.</summary>
    public ClaudeAgentOptionsBuilder WithEnv(string key, string value)
    {
        (_env ??= new Dictionary<string, string>())[key] = value;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.Cwd"/>.</summary>
    public ClaudeAgentOptionsBuilder WithCwd(string cwd)
    {
        _cwd = cwd;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.User"/>.</summary>
    public ClaudeAgentOptionsBuilder WithUser(string user)
    {
        _user = user;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.CliPath"/>.</summary>
    public ClaudeAgentOptionsBuilder WithCliPath(string path)
    {
        _cliPath = path;
        return this;
    }

    /// <summary>Register a stderr line callback.</summary>
    public ClaudeAgentOptionsBuilder OnStderr(Action<string> handler)
    {
        _stderr = handler;
        return this;
    }

    /// <summary>Append API beta features.</summary>
    public ClaudeAgentOptionsBuilder WithBetas(params SdkBeta[] betas)
    {
        (_betas ??= new List<SdkBeta>()).AddRange(betas);
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.OutputFormat"/>.</summary>
    public ClaudeAgentOptionsBuilder WithOutputFormat(OutputFormatSpec spec)
    {
        _outputFormat = spec;
        return this;
    }

    /// <summary>
    /// Add a single extra CLI argument. Use <c>null</c> for boolean flags
    /// (e.g. <c>WithExtraArg("verbose", null)</c>).
    /// </summary>
    public ClaudeAgentOptionsBuilder WithExtraArg(string key, string? value)
    {
        (_extraArgs ??= new Dictionary<string, string?>())[key] = value;
        return this;
    }

    /// <summary>Set <see cref="ClaudeAgentOptions.MaxBufferSize"/> in bytes.</summary>
    public ClaudeAgentOptionsBuilder WithMaxBufferSize(int bytes)
    {
        _maxBufferSize = bytes;
        return this;
    }

    /// <summary>Enable file checkpointing.</summary>
    public ClaudeAgentOptionsBuilder EnableFileCheckpointing()
    {
        _enableFileCheckpointing = true;
        return this;
    }

    /// <summary>Snapshot the current state into an immutable record.</summary>
    public ClaudeAgentOptions Build()
    {
        return new ClaudeAgentOptions
        {
            Model = _model,
            FallbackModel = _fallbackModel,
            AllowedTools = _allowedTools?.ToArray() ?? Array.Empty<string>(),
            DisallowedTools = _disallowedTools?.ToArray() ?? Array.Empty<string>(),
            Tools = _tools?.ToArray(),
            Skills = _skillsExplicitlyNull ? null : (IReadOnlyList<string>?)_skills?.ToArray(),
            PermissionMode = _permissionMode,
            PermissionPromptToolName = _permissionPromptToolName,
            CanUseTool = _canUseTool,
            Hooks = MaterialiseHooks(_hooks),
            McpServers = _mcpServers is null
                ? null
                : new Dictionary<string, McpServerConfig>(_mcpServers),
            Plugins = _plugins?.ToArray() ?? Array.Empty<SdkPluginConfig>(),
            Agents = _agents is null ? null : new Dictionary<string, AgentDefinition>(_agents),
            Thinking = _thinking,
            MaxThinkingTokens = _maxThinkingTokens,
            Effort = _effort,
            MaxTurns = _maxTurns,
            MaxBudgetUsd = _maxBudgetUsd,
            TaskBudget = _taskBudget,
            Resume = _resume,
            ContinueConversation = _continueConversation,
            SessionId = _sessionId,
            ForkSession = _forkSession,
            SessionStore = _sessionStore,
            IncludePartialMessages = _includePartialMessages,
            Settings = _settings,
            Sandbox = _sandbox,
            SystemPrompt = _systemPrompt,
            SettingSources = _settingSourcesExplicitlyNull
                ? null
                : (IReadOnlyList<SettingSource>?)_settingSources?.ToArray(),
            AddDirs = _addDirs?.ToArray() ?? Array.Empty<string>(),
            Env = _env is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(_env),
            Cwd = _cwd,
            User = _user,
            CliPath = _cliPath,
            Stderr = _stderr,
            Betas = _betas?.ToArray() ?? Array.Empty<SdkBeta>(),
            OutputFormat = _outputFormat,
            ExtraArgs = _extraArgs is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?>(_extraArgs),
            MaxBufferSize = _maxBufferSize,
            EnableFileCheckpointing = _enableFileCheckpointing,
        };
    }

    private static IReadOnlyDictionary<HookEvent, IReadOnlyList<HookMatcher>>? MaterialiseHooks(
        Dictionary<HookEvent, List<HookMatcher>>? source
    )
    {
        if (source is null)
        {
            return null;
        }
        var result = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>(source.Count);
        foreach (var (key, matchers) in source)
        {
            result[key] = matchers.ToArray();
        }
        return result;
    }
}
