# claude-agent-sdk-dotnet — Design Spec

**Date:** 2026-05-05
**Status:** Draft, pending user review
**Authors:** Justin Barias (with Claude)
**Repo name:** `claude-agent-sdk-dotnet`
**Local working dir:** `/Users/justinbarias/Documents/Git/claude-dotnet`

---

## 1. Purpose

A .NET wrapper SDK for the Claude Agent SDK that mirrors the Python SDK's
architecture: it spawns the Claude Code Node CLI as a subprocess and
communicates with it over `stream-json` NDJSON on stdin/stdout. The .NET
package exposes an idiomatic, fully-typed API surface that covers every
feature of the Python SDK 0.1.72 (which itself wraps Claude CLI 2.1.126).

Non-goal: reimplementing the agent loop natively in C#. The CLI is the source
of truth, exactly as it is for the Python and TypeScript SDKs.

## 2. Reference architecture (from Python SDK 0.1.72)

The Python SDK's `_internal/transport/subprocess_cli.py`:

- Locates the `claude` binary (bundled first, then `PATH`, then known npm/bin
  locations).
- Spawns `claude --output-format stream-json --input-format stream-json
  --verbose [...flags from ClaudeAgentOptions...]`.
- Pipes stdin/stdout (and stderr, only if a callback was registered).
- Sends user prompts and a control protocol over stdin as NDJSON; reads a
  message stream over stdout as NDJSON, with speculative buffer-and-parse to
  tolerate `TextReceiveStream` line truncation.
- Manages graceful shutdown: close stdin → wait 5 s → SIGTERM → wait 5 s →
  SIGKILL.
- Propagates active OpenTelemetry W3C TraceContext into the CLI's environment.
- Sets `CLAUDE_CODE_ENTRYPOINT=sdk-py` and `CLAUDE_AGENT_SDK_VERSION=<v>`;
  filters `CLAUDECODE` from inherited env (issue #573).
- Verifies CLI version >= 2.0.0 (warn-only).

Two public entry points:

- `query(prompt, options)` — async generator yielding `Message` records for
  one-shot use.
- `ClaudeSDKClient(options)` — interactive multi-turn streaming client with
  `connect`, `send_user_message`, `receive_messages`, `disconnect`.

A control protocol layered over the same stdio handles:

- Hooks: `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `Stop`,
  `SubagentStop`, `SubagentStart`, `UserPromptSubmit`, `PreCompact`,
  `Notification`, `PermissionRequest`.
- `can_use_tool` permission callback.
- In-process SDK MCP server tool calls (CLI calls back into the host process).
- Session listing/info/messages/subagents queries.
- Session mutations (`rename`, `tag`, `delete`, `fork`).
- `SessionStore` protocol for pluggable session backends.

## 3. Operating principles

- **Architecture parity** with Python: same subprocess model, same wire
  protocol, same control-protocol semantics. Internals stay close enough to
  Python's that upstream changes can be tracked mechanically.
- **API idiomaticity**: PascalCase, `IAsyncEnumerable<T>` streaming, records
  with `with`-expressions, fluent builders for ergonomics, attributes for MCP
  via the official `ModelContextProtocol` SDK, `CancellationToken` everywhere,
  `*Async` suffix on `Task`-returning methods.
- **Layered packaging**: a small dependency-free core; opt-in MCP, DI, and
  Testing companions; native bundle delivered via runtime-specific NuGets.
- **AOT-friendly core**: `<IsAotCompatible>true</IsAotCompatible>`, STJ
  source generation for the wire protocol, no reflection on hot paths.

## 4. Target framework & toolchain

- `net10.0` only. .NET 10 is the current LTS (GA Nov 2025; LTS through Nov
  2028) and is the appropriate baseline for a library starting in mid-2026.
- C# 14, nullable reference types enabled, file-scoped namespaces, implicit
  usings off (explicit imports for clarity).
- Central Package Management via `Directory.Packages.props`.
- `global.json` pins the .NET 10 SDK band.
- `Directory.Build.props` enforces: `Nullable=enable`, `TreatWarningsAsErrors=true`,
  `IsAotCompatible=true` (core), Microsoft.CodeAnalysis.NetAnalyzers,
  StyleCop.Analyzers.

## 5. NuGet packages

| Package | Depends on | Purpose |
|---|---|---|
| `Daystrom.ClaudeAgentSdk` | STJ, `Microsoft.Extensions.Logging.Abstractions`, runtime native sub-packages | Core: `ClaudeAgent.QueryAsync`, `IClaudeAgentClient`, options, messages, hooks, permissions, sessions, transport, control protocol |
| `Daystrom.ClaudeAgentSdk.Mcp` | core + `ModelContextProtocol` | In-process MCP server helpers (attribute + fluent) |
| `Daystrom.ClaudeAgentSdk.DependencyInjection` | core + `Microsoft.Extensions.DependencyInjection.Abstractions` + `Microsoft.Extensions.Options.ConfigurationExtensions` | `services.AddClaudeAgent()`, `IOptions<>` binding, `AddClaudeAgentHook<T>()` |
| `Daystrom.ClaudeAgentSdk.Testing` | core | `FakeTransport`, `RecordingTransport`, `ClaudeAgentClientHarness` |
| `runtime.{rid}.Daystrom.ClaudeAgentSdk.Native` × 5 RIDs | (none) | The pinned `claude` Node binary for one RID, packed under `runtimes/{rid}/native/` |

Supported RIDs: `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.

## 6. Repository layout

```
claude-agent-sdk-dotnet/
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── claude-agent-sdk-dotnet.sln
├── LICENSE              (MIT)
├── README.md
├── CHANGELOG.md
├── RELEASING.md
├── nuget.config
├── src/
│   ├── Daystrom.ClaudeAgentSdk/
│   ├── Daystrom.ClaudeAgentSdk.Mcp/
│   ├── Daystrom.ClaudeAgentSdk.DependencyInjection/
│   ├── Daystrom.ClaudeAgentSdk.Testing/
│   └── runtimes/
│       ├── runtime.win-x64.Daystrom.ClaudeAgentSdk.Native/
│       ├── runtime.linux-x64.Daystrom.ClaudeAgentSdk.Native/
│       ├── runtime.linux-arm64.Daystrom.ClaudeAgentSdk.Native/
│       ├── runtime.osx-x64.Daystrom.ClaudeAgentSdk.Native/
│       └── runtime.osx-arm64.Daystrom.ClaudeAgentSdk.Native/
├── tests/
│   ├── Daystrom.ClaudeAgentSdk.Tests/
│   ├── Daystrom.ClaudeAgentSdk.Mcp.Tests/
│   ├── Daystrom.ClaudeAgentSdk.DependencyInjection.Tests/
│   └── Daystrom.ClaudeAgentSdk.IntegrationTests/
├── samples/
│   ├── QuickStart/
│   ├── StreamingMode/
│   ├── McpCalculator/
│   ├── Hooks/
│   ├── ToolPermissionCallback/
│   ├── Agents/
│   ├── Plugin/
│   ├── SessionResume/
│   └── DependencyInjection/
├── eng/
│   ├── download-claude-cli.ps1     # downloads pinned CLI per RID at pack time
│   ├── pack-native.ps1
│   └── Versions.props              # ClaudeCliVersion + SDK version
├── .github/workflows/
│   ├── ci.yml
│   ├── release.yml
│   └── native-bundle.yml
└── docs/
    ├── architecture.md
    └── superpowers/specs/
```

## 7. Core project layout (`src/Daystrom.ClaudeAgentSdk/`)

Mirrors `claude_agent_sdk/_internal/` plus a public top-level surface.

```
ClaudeAgent.cs                       # static entry: ClaudeAgent.QueryAsync(...)
IClaudeAgentClient.cs                # interactive multi-turn
ClaudeAgentClient.cs                 # impl, async-disposable
ClaudeAgentOptions.cs                # record + fluent ClaudeAgentOptionsBuilder
PermissionMode.cs                    # enum: Default, AcceptEdits, BypassPermissions, Plan, DontAsk, Auto
SettingSource.cs                     # enum: User, Project, Local
SdkBeta.cs

Messages/
  Message.cs                         # abstract record + JsonPolymorphic discriminator
  AssistantMessage.cs
  UserMessage.cs
  SystemMessage.cs
  ResultMessage.cs
  StreamEvent.cs
  TaskStartedMessage.cs
  TaskProgressMessage.cs
  TaskNotificationMessage.cs
  RateLimitInfo.cs / RateLimitEvent.cs / RateLimitStatus.cs / RateLimitType.cs
  ContextUsageResponse.cs / ContextUsageCategory.cs
  TaskUsage.cs / TaskBudget.cs
  Content/
    ContentBlock.cs                  # abstract record
    TextBlock.cs ThinkingBlock.cs
    ToolUseBlock.cs ToolResultBlock.cs
    ServerToolUseBlock.cs ServerToolResultBlock.cs
    ServerToolName.cs                # enum

Hooks/
  HookHandler.cs                     # delegate type
  IHookHandler.cs / IHookHandler<TInput>.cs    # for class-based handlers
  HookContext.cs HookMatcher.cs HookResult.cs HookJsonOutput.cs
  Inputs/
    HookInput.cs (abstract)
    PreToolUseHookInput.cs
    PostToolUseHookInput.cs
    PostToolUseFailureHookInput.cs
    UserPromptSubmitHookInput.cs
    StopHookInput.cs
    SubagentStopHookInput.cs
    SubagentStartHookInput.cs
    PreCompactHookInput.cs
    NotificationHookInput.cs
    PermissionRequestHookInput.cs
  Outputs/
    NotificationHookSpecificOutput.cs
    SubagentStartHookSpecificOutput.cs
    PermissionRequestHookSpecificOutput.cs
    PostToolUseFailureHookSpecificOutput.cs

Permissions/
  CanUseToolDelegate.cs
  ToolPermissionContext.cs
  PermissionResult.cs                # abstract + Allow / Deny records
  PermissionUpdate.cs

Sessions/
  SessionsClient.cs                  # static API (list/get/messages/...)
  ISessionStore.cs
  InMemorySessionStore.cs
  SessionKey.cs SessionStoreEntry.cs SessionStoreFlushMode.cs
  SessionStoreListEntry.cs SessionListSubkeysKey.cs
  SessionSummaryEntry.cs SessionMessage.cs SDKSessionInfo.cs
  ForkSessionResult.cs MirrorErrorMessage.cs
  SessionImporter.cs                 # ImportSessionToStoreAsync
  SessionSummaryFolder.cs            # FoldSessionSummary

Agents/
  AgentDefinition.cs
  Plugins/
    SdkPluginConfig.cs

Mcp/                                 # config types only — instances live in .Mcp package
  McpServerConfig.cs (abstract)
  McpStdioServerConfig.cs
  McpHttpServerConfig.cs
  McpSseServerConfig.cs
  McpSdkServerConfig.cs              # holds an opaque IMcpServerInstance
  IMcpServerInstance.cs              # bridge to .Mcp package
  McpServerStatus.cs McpServerStatusConfig.cs McpServerConnectionStatus.cs
  McpServerInfo.cs McpStatusResponse.cs
  McpToolInfo.cs McpToolAnnotations.cs

Sandbox/
  SandboxSettings.cs SandboxNetworkConfig.cs SandboxIgnoreViolations.cs
  # Platform note: the CLI does not implement sandboxing on native Windows
  # (only on macOS, Linux, and WSL2). Setting `Sandbox` on a native Windows
  # host is a no-op — the SDK still serializes the settings into the CLI
  # `--settings` payload, but the CLI ignores them. SandboxSettings XML
  # doc-comments must call this out, and the CliBinaryResolver / runtime
  # detection should `ILogger.LogWarning` once per process start when
  # Sandbox is non-null and the host is native Windows.

ThinkingConfig/
  ThinkingConfig.cs (abstract)
  ThinkingConfigAdaptive.cs ThinkingConfigEnabled.cs ThinkingConfigDisabled.cs

Transport/
  ITransport.cs                      # public extension point
  SubprocessCliTransport.cs          # default, mirrors Python
  CliBinaryResolver.cs
  NdjsonReader.cs

Control/
  ControlProtocol.cs                 # initialize, hook callbacks, can_use_tool, MCP routing
  ControlMessages.cs

Internal/
  MessageParser.cs
  CommandBuilder.cs                  # ClaudeAgentOptions → CLI argv (streaming + one-shot modes)
  ControlProtocolGate.cs             # NeedsControlProtocol(options) predicate
  OtelContextInjector.cs
  ProcessGracefulShutdown.cs

Errors/
  ClaudeSdkException.cs              # base
  CliConnectionException.cs
  CliNotFoundException.cs
  ProcessException.cs                # ExitCode, Stderr
  CliJsonDecodeException.cs          # RawLine + inner JsonException

Json/
  ClaudeAgentJsonContext.cs          # source-gen STJ context
```

## 8. Public API contracts

### Top-level static entry

```csharp
public static class ClaudeAgent
{
    public static IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    public static IAsyncEnumerable<Message> QueryAsync(
        IAsyncEnumerable<UserMessageInput> prompts,
        ClaudeAgentOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);
}
```

### Interactive client

```csharp
public interface IClaudeAgentClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct = default);
    Task SendUserMessageAsync(string text, CancellationToken ct = default);
    Task SendUserMessageAsync(UserMessageInput message, CancellationToken ct = default);
    IAsyncEnumerable<Message> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken ct = default);
    Task EndInputAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    // Control-plane operations (proxied to CLI)
    Task<McpStatusResponse> GetMcpStatusAsync(CancellationToken ct = default);
    Task<ContextUsageResponse> GetContextUsageAsync(CancellationToken ct = default);
    Task ApplyPermissionUpdateAsync(PermissionUpdate update, CancellationToken ct = default);
    Task InterruptAsync(CancellationToken ct = default);
}

public sealed class ClaudeAgentClient : IClaudeAgentClient
{
    public static IClaudeAgentClient Create(ClaudeAgentOptions options, ITransport? transport = null);
}
```

### Options & fluent builder

```csharp
public sealed record ClaudeAgentOptions
{
    public string? Model { get; init; }
    public string? FallbackModel { get; init; }
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public IReadOnlyList<string> DisallowedTools { get; init; } = [];
    public IReadOnlyList<string>? Tools { get; init; }       // base tool set
    public IReadOnlyList<string>? Skills { get; init; }      // null | "all" | list
    public PermissionMode? PermissionMode { get; init; }
    public string? PermissionPromptToolName { get; init; }
    public CanUseToolDelegate? CanUseTool { get; init; }
    public IReadOnlyDictionary<HookEvent, IReadOnlyList<HookMatcher>>? Hooks { get; init; }
    public IReadOnlyDictionary<string, McpServerConfig>? McpServers { get; init; }
    public IReadOnlyList<SdkPluginConfig> Plugins { get; init; } = [];
    public IReadOnlyDictionary<string, AgentDefinition>? Agents { get; init; }
    public ThinkingConfig? Thinking { get; init; }
    public int? MaxThinkingTokens { get; init; }             // deprecated
    public string? Effort { get; init; }
    public int? MaxTurns { get; init; }
    public decimal? MaxBudgetUsd { get; init; }
    public TaskBudget? TaskBudget { get; init; }
    public string? Resume { get; init; }
    public bool ContinueConversation { get; init; }
    public string? SessionId { get; init; }
    public bool ForkSession { get; init; }
    public ISessionStore? SessionStore { get; init; }
    public bool IncludePartialMessages { get; init; }
    public string? Settings { get; init; }                   // JSON or path
    public SandboxSettings? Sandbox { get; init; }
    public SystemPromptSpec? SystemPrompt { get; init; }     // string | preset | file
    public IReadOnlyList<SettingSource>? SettingSources { get; init; }
    public IReadOnlyList<string> AddDirs { get; init; } = [];
    public IReadOnlyDictionary<string, string> Env { get; init; } = ImmutableDictionary<string,string>.Empty;
    public string? Cwd { get; init; }
    public string? User { get; init; }
    public string? CliPath { get; init; }
    public Action<string>? Stderr { get; init; }
    public IReadOnlyList<string> Betas { get; init; } = [];
    public OutputFormatSpec? OutputFormat { get; init; }     // json_schema { schema = ... }
    public IReadOnlyDictionary<string, string?> ExtraArgs { get; init; } = ImmutableDictionary<string,string?>.Empty;
    public int? MaxBufferSize { get; init; }
    public bool EnableFileCheckpointing { get; init; }

    public static ClaudeAgentOptionsBuilder Create() => new();
}

public sealed class ClaudeAgentOptionsBuilder
{
    public ClaudeAgentOptionsBuilder WithModel(string model);
    public ClaudeAgentOptionsBuilder WithFallbackModel(string model);
    public ClaudeAgentOptionsBuilder AllowTools(params string[] tools);
    public ClaudeAgentOptionsBuilder DisallowTools(params string[] tools);
    public ClaudeAgentOptionsBuilder WithPermissionMode(PermissionMode mode);
    public ClaudeAgentOptionsBuilder OnCanUseTool(CanUseToolDelegate handler);
    public ClaudeAgentOptionsBuilder OnPreToolUse(string matcher, HookHandler<PreToolUseHookInput> handler);
    public ClaudeAgentOptionsBuilder OnPostToolUse(string matcher, HookHandler<PostToolUseHookInput> handler);
    // ... one On* method per hook event
    public ClaudeAgentOptionsBuilder AddMcpServer(string name, McpServerConfig config);
    public ClaudeAgentOptionsBuilder AddAgent(string name, AgentDefinition definition);
    public ClaudeAgentOptionsBuilder AddPlugin(SdkPluginConfig plugin);
    public ClaudeAgentOptionsBuilder WithThinking(ThinkingConfig thinking);
    public ClaudeAgentOptionsBuilder WithSandbox(SandboxSettings sandbox);
    public ClaudeAgentOptionsBuilder WithSessionStore(ISessionStore store);
    public ClaudeAgentOptionsBuilder WithCwd(string cwd);
    public ClaudeAgentOptionsBuilder WithEnv(string key, string value);
    public ClaudeAgentOptionsBuilder OnStderr(Action<string> handler);
    public ClaudeAgentOptions Build();
}
```

### Hooks

```csharp
// Strongly-typed per-event delegate (consumer-facing).
public delegate Task<HookResult> HookHandler<TInput>(
    TInput input, HookContext context, CancellationToken cancellationToken)
    where TInput : HookInput;

// Non-generic erased form stored on the wire — delegates are not covariant in
// their input parameter, so the builder wraps strongly-typed handlers in this
// erased form before adding them to a HookMatcher.
public delegate Task<HookResult> HookHandler(
    HookInput input, HookContext context, CancellationToken cancellationToken);

public enum HookEvent
{
    PreToolUse, PostToolUse, PostToolUseFailure,
    UserPromptSubmit, Stop, SubagentStop, SubagentStart,
    PreCompact, Notification, PermissionRequest
}

public interface IHookHandler<TInput> where TInput : HookInput
{
    Task<HookResult> HandleAsync(TInput input, HookContext context, CancellationToken ct);
}

public sealed record HookMatcher(string Pattern, IReadOnlyList<HookHandler> Handlers);

public abstract record HookResult
{
    public sealed record Continue() : HookResult;
    public sealed record Block(string Reason) : HookResult;
    public sealed record Modify(JsonElement OutputJson) : HookResult;
}
```

### Permissions

```csharp
public delegate Task<PermissionResult> CanUseToolDelegate(
    string toolName,
    JsonElement input,
    ToolPermissionContext context,
    CancellationToken cancellationToken);

public abstract record PermissionResult
{
    public sealed record Allow(JsonElement? UpdatedInput = null) : PermissionResult;
    public sealed record Deny(string Reason, bool Interrupt = false) : PermissionResult;
}
```

### MCP (in `Daystrom.ClaudeAgentSdk.Mcp`)

```csharp
public static class SdkMcpServer
{
    public static McpSdkServerConfig FromType<T>(string name, string version = "1.0.0");
    public static McpSdkServerConfig FromType(string name, Type tools, string version = "1.0.0");
    public static SdkMcpServerBuilder Create(string name, string version = "1.0.0");
}

public sealed class SdkMcpServerBuilder
{
    public SdkMcpServerBuilder AddTool<TIn, TOut>(string name, string description, Func<TIn, CancellationToken, Task<TOut>> handler);
    public SdkMcpServerBuilder AddTool(string name, string description, Delegate handler); // reflection-based
    public McpSdkServerConfig Build();
}
```

The attribute path delegates entirely to `ModelContextProtocol`'s
`[McpServerToolType]` / `[McpServerTool]`; the `.Mcp` package wraps the
resulting MCP `IMcpServer` instance into `IMcpServerInstance` for the core
`McpSdkServerConfig`. The `ControlProtocol` in the core package routes CLI
tool-call requests to this instance and serializes the response back.

### DI (in `Daystrom.ClaudeAgentSdk.DependencyInjection`)

```csharp
public static class ClaudeAgentServiceCollectionExtensions
{
    public static IServiceCollection AddClaudeAgent(this IServiceCollection services);
    public static IServiceCollection AddClaudeAgent(this IServiceCollection services, Action<ClaudeAgentOptions> configure);
    public static IServiceCollection AddClaudeAgent(this IServiceCollection services, IConfiguration configuration);
    public static IServiceCollection AddClaudeAgentHook<THandler>(this IServiceCollection services, string @event, string matcher = "*")
        where THandler : class, IHookHandler<HookInput>;
}
```

Registers `IClaudeAgentClient` as scoped (interactive multi-turn) and a
`ClaudeAgentFactory` as singleton (creates one-shot `query` enumerables).

### Errors

```csharp
public abstract class ClaudeSdkException : Exception { ... }
public sealed class CliConnectionException  : ClaudeSdkException { ... }
public sealed class CliNotFoundException    : ClaudeSdkException { ... }
public sealed class ProcessException        : ClaudeSdkException { public int ExitCode; public string? Stderr; }
public sealed class CliJsonDecodeException  : ClaudeSdkException { public string RawLine; }
```

## 9. Wire protocol details

### 9.0 One-shot fast path (`--print` mode)

`ClaudeAgent.QueryAsync(string, ...)` branches on a
`ControlProtocolGate.NeedsControlProtocol(options)` predicate. The streaming
control-protocol path is only spun up when the caller actually needs it;
otherwise we use the CLI's classic non-interactive `--print` mode.

`NeedsControlProtocol` returns `true` if **any** of the following hold:

- `options.CanUseTool` is non-null.
- `options.Hooks` is non-null and non-empty.
- `options.McpServers` contains at least one `McpSdkServerConfig` (in-process
  MCP — stdio/http/sse MCP configs do **not** need the control protocol; the
  CLI talks to those directly).
- A custom `ITransport` was supplied (callers with a custom transport always
  go through the streaming path so the transport sees the full protocol).

When the predicate returns `false`, `QueryAsync` runs the one-shot shape:

```
claude [...flags...] --output-format stream-json --verbose --print -- "<prompt>"
```

- Stdin is closed immediately after spawn.
- No `initialize` control-request handshake, no `QueryHandler`/`Channel`
  plumbing, no in-process write lock, no `StreamInputAsync` task.
- The transport reads NDJSON from stdout until EOF, parses each line into a
  `Message`, and yields it. Process exit replaces graceful shutdown.
- `ResultMessage` shape is identical to the streaming path because output
  format is `stream-json --verbose` in both modes; the parser is shared.
- The `IAsyncEnumerable<UserMessageInput>` overload of `QueryAsync` and all
  of `IClaudeAgentClient` always use the streaming path.

Rationale (vs. Python parity): Python's `query()` always uses streaming. The
fast path is additive — it changes nothing about the wire format the CLI
emits, only the input mode and the SDK-side machinery. This delivers a
meaningful latency win on short one-shot calls (no init round-trip) and
shrinks the surface area that a caller without hooks/permissions/in-proc-MCP
has to trust.

### 9.1 Argument construction

- Spawn argv built by `Internal/CommandBuilder.cs`, mirroring Python's
  `_build_command()` exactly, including: `--system-prompt[-file]`,
  `--append-system-prompt`, `--tools`, `--allowedTools`, `--disallowedTools`,
  `--max-turns`, `--max-budget-usd`, `--task-budget`, `--model`,
  `--fallback-model`, `--betas`, `--permission-prompt-tool`,
  `--permission-mode`, `--continue`, `--resume`, `--session-id`,
  `--settings`, `--add-dir`, `--mcp-config`, `--include-partial-messages`,
  `--fork-session`, `--session-mirror`, `--setting-sources=`,
  `--plugin-dir`, `--thinking[ adaptive|disabled ]`,
  `--max-thinking-tokens`, `--thinking-display`, `--effort`, `--json-schema`,
  `--output-format stream-json --verbose`.
  Input mode is mode-dependent: streaming path appends
  `--input-format stream-json`; one-shot path appends `--print -- "<prompt>"`
  as the final tokens.
- Skills defaults applied identically to Python (`Skill` / `Skill(name)`
  injection + `setting_sources` defaulting to `user,project`).
- Sandbox merged into `--settings` JSON identically.
- Outbound `control_request` subtypes used by the SDK: `initialize`,
  `interrupt` (sent by `IClaudeAgentClient.InterruptAsync`),
  `set_permission_mode`, `mcp_message`, hook callbacks, `can_use_tool`.
- Inbound `control_cancel_request` (CLI → SDK, "abandon any pending
  callback for `request_id` X") is **received but not acted upon** in v1,
  matching Python and other reference SDKs. See §13 for the semantics.
- Stdin send protected by an async lock (matching Python's `_write_lock`).
- Stdout: speculative buffer-and-parse with configurable max buffer
  (default 1 MiB).
- Graceful shutdown: stdin EOF → `WaitForExitAsync(5s)` → `Process.Kill(false)`
  → `WaitForExitAsync(5s)` → `Process.Kill(true)`.
- OTel: scrub inherited `TRACEPARENT`/`TRACESTATE` if a fresh `Activity` is
  active (unless explicitly set in `Options.Env`); inject from
  `Activity.Current` via `DistributedContextPropagator.Current`.
- Env: `CLAUDE_CODE_ENTRYPOINT=sdk-dotnet`, `CLAUDE_AGENT_SDK_VERSION=<v>`;
  filter inherited `CLAUDECODE`.
- CLI version check: parse `claude -v`, warn if `< 2.0.0`, gated behind
  `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK` env var.

## 10. Native bundle pipeline

- `eng/Versions.props` carries `<ClaudeCliVersion>2.1.126</ClaudeCliVersion>`
  pinned per SDK release.
- `eng/download-claude-cli.ps1` (cross-platform PowerShell):
  1. `npm pack @anthropic-ai/claude-code@$(ClaudeCliVersion)` for each RID.
  2. Extract per-RID native binary.
  3. Drop into `src/runtimes/runtime.{rid}.Daystrom.ClaudeAgentSdk.Native/runtimes/{rid}/native/claude{,.exe}`.
- Each `runtime.{rid}.Daystrom.ClaudeAgentSdk.Native.csproj` is
  `<IncludeBuildOutput>false</IncludeBuildOutput>` and packs only the
  `runtimes/{rid}/native/**` payload.
- `Daystrom.ClaudeAgentSdk.csproj` references all five with
  `<PrivateAssets>none</PrivateAssets>` and `<IncludeAssets>runtime;native</IncludeAssets>`.
  NuGet/MSBuild resolves only the matching RID at publish/restore.
- `CliBinaryResolver` lookup order:
  1. `options.CliPath` if set.
  2. `Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", "claude{,.exe}")`.
  3. `claude` on `PATH`.
  4. Same fallback list Python uses (`~/.npm-global/bin/claude`,
     `/usr/local/bin/claude`, `~/.local/bin/claude`,
     `~/node_modules/.bin/claude`, `~/.yarn/bin/claude`,
     `~/.claude/local/claude`).

## 11. JSON / serialization

- A single `ClaudeAgentJsonContext : JsonSerializerContext` declares every
  wire type with `[JsonSerializable]`.
- Discriminated unions (Message, ContentBlock, HookInput, McpServerConfig,
  ThinkingConfig, PermissionResult) use `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]`
  with `[JsonDerivedType(typeof(...), typeDiscriminator: "...")]` per variant.
- Snake-case wire names handled by `JsonNamingPolicy.SnakeCaseLower` set on
  the context options (matches CLI conventions). Where a property name
  would collide with a C# keyword we use `[JsonPropertyName]`.
- Source-gen output is trim/AOT safe; the core package is `IsAotCompatible`.
- The `.Mcp` package's reflection-based fluent `AddTool(Delegate)` is
  annotated `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`; the
  attribute path is annotation-free since it uses MCP SDK's source generator.

## 12. Logging & observability

- `ILoggerFactory` resolved from `ClaudeAgentOptions.LoggerFactory` (optional)
  or DI; defaults to `NullLoggerFactory.Instance`.
- One logger per major component: `Transport`, `Control`, `MessageParser`,
  `Hooks`, `CliResolver`.
- `EventSource` (`Daystrom-ClaudeAgentSdk`) emits start/stop/cancel events
  for `query` and per-hook invocations — picked up by `dotnet-trace` and
  OpenTelemetry's `EventSourceListener`.
- Active `Activity` is captured around `query()` and hook invocations so
  spans nest correctly under user code.

## 13. Cancellation & lifetime

Three distinct concepts; treat them as separate axes:

1. **Caller-side `CancellationToken`** (.NET → SDK). Every public async method
   takes one. Cancelling an `await foreach (var m in QueryAsync(..., ct))`
   cancels stdin writes, closes stdin, and triggers the graceful-shutdown
   sequence. `IClaudeAgentClient` is `IAsyncDisposable`; `DisposeAsync` runs
   the shutdown sequence and is safe to call multiple times.

2. **Outbound interrupt** (SDK → CLI). `IClaudeAgentClient.InterruptAsync`
   sends a `control_request { subtype: "interrupt" }` over stdin and awaits
   the CLI's ack. This is the public "stop the agent's current turn" API,
   mirroring Python's `client.interrupt()`. Available only on the streaming
   client — `ClaudeAgent.QueryAsync` is one-shot and has no interrupt
   semantics (cancel the token instead).

3. **Inbound `control_cancel_request`** (CLI → SDK). The CLI sends this when
   it no longer needs the result of a `control_request` it issued earlier
   (typically a hook callback or `can_use_tool` prompt whose answer is
   moot — e.g., the user aborted before the callback returned). In v1 the
   SDK reads the message, discards it, and lets any in-flight callback run
   to completion; the late result is ignored on its way back. This matches
   the Python SDK and 0xeb's reference implementation, both of which carry
   the same TODO. **Implication for callback authors:** do not assume that a
   hook callback being invoked means its result will be consumed. Hooks
   should be idempotent and side-effect-light, or guard their side effects
   with their own cancellation logic. Wiring this up properly is on the
   roadmap but not a v1 commitment.

## 14. Testing

- **Unit tests** (xUnit + `Verify.Xunit` for snapshot assertions):
  - `CommandBuilderTests`: assert exact argv emitted for each option permutation.
  - `MessageParserTests`: round-trip every Python-fixture sample through STJ.
  - `NdjsonReaderTests`: partial-line truncation, max-buffer overflow.
  - `ControlProtocolTests`: `FakeTransport` scripts hook callbacks and MCP
    tool-call requests; assert correct responses.
  - `CliBinaryResolverTests`: in-memory filesystem.
  - Fluent builder tests.
- **MCP tests**: round-trip an attribute-decorated class through `SdkMcpServer.FromType<T>`,
  drive a tool call via `FakeTransport`, assert serialized result.
- **DI tests**: `ServiceCollection.AddClaudeAgent` wiring, `IConfiguration`
  binding, `AddClaudeAgentHook<T>` registration.
- **Integration tests**: gated behind `CLAUDE_INTEGRATION=1` env, run only
  when a real `ANTHROPIC_API_KEY` is present in CI; cover quick-start, hooks,
  in-process MCP, sessions, plugin loading.
- **Sample-as-test**: each `samples/*` project compiles in CI; smoke samples
  run under `--api-key=$DUMMY` against a stub.

## 15. CI/CD

- `ci.yml`: matrix across `ubuntu-latest`, `windows-latest`, `macos-latest`;
  `dotnet test --collect:"XPlat Code Coverage"`; integration job runs on
  Linux only with secret-gated key.
- `native-bundle.yml`: runs on release tags only (and on demand via
  `workflow_dispatch`); downloads and packs the five runtime sub-packages,
  attaches as artifacts. The CLI version is pinned per release so nightly
  rebuilds would be wasted work.
- `release.yml`: triggered by tag `v*`; runs full build + test + pack +
  `dotnet nuget push` to nuget.org for all five user-facing packages and the
  native sub-packages.

## 16. Versioning

- Semver. `0.x` while we track the Python SDK's pre-1.0 churn.
- The .NET SDK version is independent of `ClaudeCliVersion`; both are listed
  in the package readme and `eng/Versions.props`.
- Sub-packages move in lockstep with the core via Central Package Management.
- Migration notes go in `CHANGELOG.md`; breaking-change PRs touch
  `RELEASING.md` checklist.

## 17. Out of scope (v1)

- Reimplementing the agent loop natively in C#.
- Custom non-stdio transports (HTTP, gRPC) shipped in-box — `ITransport` is
  public so users can build their own.
- F#/VB-specific helper packages.
- A separate `Hosting` package with `IHostedService`-based agents — defer
  until demand emerges; users can wrap `IClaudeAgentClient` themselves.
- Built-in OpenTelemetry tracing package (the BCL `Activity` propagation
  is in core; an `Daystrom.ClaudeAgentSdk.OpenTelemetry` package can come
  later if needed).

## 18. Acceptance criteria

A release is shippable when:

1. All public types in §7's tree exist with XML doc comments.
2. `Daystrom.ClaudeAgentSdk` is `IsAotCompatible=true` with zero trim warnings.
3. `dotnet test` passes on Linux, macOS, Windows in CI.
4. `samples/QuickStart` runs end-to-end against the bundled CLI on a clean
   machine with no extra installs after `dotnet add package Daystrom.ClaudeAgentSdk`.
5. The feature parity matrix in §19 is 100% green.
6. `dotnet pack` produces all four user-facing packages and all five RID
   native sub-packages, each ≤ the size budget noted in `RELEASING.md`.
7. README has a one-page quick start matching the Python overview's three
   examples (filesystem read, hooks, MCP).
8. All packages are built deterministic (`Deterministic=true`,
   `ContinuousIntegrationBuild=true` in CI) with embedded Source Link
   (`PublishRepositoryUrl=true`, `EmbedUntrackedSources=true`), a generated
   SBOM (`Microsoft.Sbom.Targets`), and signed with the project's NuGet
   signing certificate before push.

## 19. Feature parity matrix

| Python symbol (claude_agent_sdk) | .NET equivalent | Package |
|---|---|---|
| `query` | `ClaudeAgent.QueryAsync` | core |
| `ClaudeSDKClient` | `IClaudeAgentClient` / `ClaudeAgentClient` | core |
| `ClaudeAgentOptions` | `ClaudeAgentOptions` + `ClaudeAgentOptionsBuilder` | core |
| `Transport` | `ITransport` | core |
| `__version__` | `ClaudeAgent.SdkVersion` | core |
| `PermissionMode` | enum `PermissionMode` | core |
| `SettingSource` | enum `SettingSource` | core |
| `SdkBeta` | `SdkBeta` | core |
| `SdkPluginConfig` | `SdkPluginConfig` | core |
| `AgentDefinition` | `AgentDefinition` | core |
| `Message`, `UserMessage`, `AssistantMessage`, `SystemMessage`, `ResultMessage`, `StreamEvent` | record `Message` + variants | core |
| `TaskStartedMessage`, `TaskProgressMessage`, `TaskNotificationMessage`, `TaskNotificationStatus`, `TaskUsage`, `TaskBudget` | same names | core |
| `RateLimitInfo`, `RateLimitStatus`, `RateLimitType`, `RateLimitEvent` | same names | core |
| `ContextUsageResponse`, `ContextUsageCategory` | same names | core |
| `TextBlock`, `ThinkingBlock`, `ToolUseBlock`, `ToolResultBlock`, `ServerToolUseBlock`, `ServerToolResultBlock`, `ContentBlock`, `ServerToolName` | records under `Messages/Content/` | core |
| `ThinkingConfig{Adaptive,Enabled,Disabled}` | discriminated record `ThinkingConfig` | core |
| `CanUseTool`, `ToolPermissionContext`, `PermissionResult{,Allow,Deny}`, `PermissionUpdate` | same names | core |
| `HookCallback`, `HookContext`, `HookInput`, `BaseHookInput`, `HookMatcher`, `HookJSONOutput` | `HookHandler<TInput>` + records | core |
| `PreToolUseHookInput`, `PostToolUseHookInput`, `PostToolUseFailureHookInput`, `UserPromptSubmitHookInput`, `StopHookInput`, `SubagentStopHookInput`, `PreCompactHookInput`, `NotificationHookInput`, `SubagentStartHookInput`, `PermissionRequestHookInput` | records under `Hooks/Inputs/` | core |
| `NotificationHookSpecificOutput`, `SubagentStartHookSpecificOutput`, `PermissionRequestHookSpecificOutput`, `PostToolUseFailureHookSpecificOutput` | records under `Hooks/Outputs/` | core |
| `McpServerConfig`, `McpSdkServerConfig`, `McpServerStatus`, `McpServerStatusConfig`, `McpServerConnectionStatus`, `McpServerInfo`, `McpStatusResponse`, `McpToolAnnotations`, `McpToolInfo` | same names | core |
| `tool` decorator, `create_sdk_mcp_server`, `SdkMcpTool`, `ToolAnnotations` | `[McpServerTool]` (delegated) + `SdkMcpServer.{FromType,Create}` | mcp |
| `list_sessions`, `get_session_info`, `get_session_messages`, `list_subagents`, `get_subagent_messages` | `SessionsClient.*Async` | core |
| `list_*_from_store`, `*_via_store` (async store-backed variants) | `SessionsClient.*FromStoreAsync`, `*ViaStoreAsync` | core |
| `rename_session`, `tag_session`, `delete_session`, `fork_session`, `ForkSessionResult` | `SessionsClient` | core |
| `SessionStore`, `SessionStoreEntry`, `SessionStoreFlushMode`, `SessionStoreListEntry`, `InMemorySessionStore`, `SessionKey`, `SessionListSubkeysKey`, `SessionSummaryEntry`, `SessionMessage`, `SDKSessionInfo`, `MirrorErrorMessage`, `project_key_for_directory`, `import_session_to_store`, `fold_session_summary` | same names | core |
| `SandboxSettings`, `SandboxNetworkConfig`, `SandboxIgnoreViolations` | same names | core |
| `ClaudeSDKError`, `CLIConnectionError`, `CLINotFoundError`, `ProcessError`, `CLIJSONDecodeError` | `ClaudeSdkException` + 4 subtypes | core |
| OTel context propagation | `OtelContextInjector` (BCL `Activity`) | core |
| Bundled CLI | `runtime.{rid}.Daystrom.ClaudeAgentSdk.Native` × 5 | native |
| `claude_agent_sdk.testing` | `Daystrom.ClaudeAgentSdk.Testing` | testing |
| (none) | `services.AddClaudeAgent()`, `IOptions<>` binding, `AddClaudeAgentHook<T>()` | dependency-injection |

## 20. Open items for follow-up plan

To be addressed in the implementation plan (writing-plans skill):

- Order of slice delivery: native bundle → core types → command builder →
  transport → message parser → query → control protocol → hooks →
  permissions → sessions → MCP → DI → testing → samples.
- Exact `[JsonPolymorphic]` discriminator strings per message variant
  (need to read CLI emission samples / Python fixtures).
- Bench targets for STJ source-gen vs. reflection serialization.
- README structure and migration-from-Python guide.
