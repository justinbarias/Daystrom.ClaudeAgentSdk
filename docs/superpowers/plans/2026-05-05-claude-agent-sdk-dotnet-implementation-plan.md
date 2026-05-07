# Implementation Plan: claude-agent-sdk-dotnet

**Date:** 2026-05-05
**Status:** Draft, pending user review
**Spec:** [`2026-05-05-claude-agent-sdk-dotnet-design.md`](../specs/2026-05-05-claude-agent-sdk-dotnet-design.md)
**Target:** `net10.0`, C# 14, NuGet release `0.1.0`

---

## Overview

Stand up `claude-agent-sdk-dotnet` in 16 phases, sliced vertically so every
phase ends in a runnable, testable deliverable rather than a half-built layer.
The first user-visible milestone is **end of Phase 5** — `ClaudeAgent.QueryAsync`
running a one-shot `--print` query against the bundled CLI. Every later phase
adds a feature *slice* (hooks, permissions, sessions, MCP, …) on top of that
working baseline rather than a horizontal layer that nothing yet consumes.

## Architecture decisions baked into this plan

These are taken from the spec — re-stated here so the plan is self-contained:

- **One-shot fast path is real, not a future optimization.** Phase 5 ships
  `--print` mode; control-protocol plumbing in Phase 6 is purely additive.
  This means we get a working SDK with zero control-protocol code.
- **Native bundle moves on its own track (Phase 2).** It only needs the spec's
  pinned CLI version and RID layout — no SDK code. We do it early so Phase 5
  and onward can run end-to-end against a real bundled binary.
- **JSON wire types come before transport.** The `ClaudeAgentJsonContext`
  source-gen context + `[JsonPolymorphic]` discriminators are foundation —
  transport/parser code is written *against* the contract, not the other way
  around.
- **Hooks, permissions, MCP, sessions are independent vertical slices.** Each
  ships its own CLI flags, control-protocol message subtypes, builder helpers,
  and tests. They can be parallelized across multiple sessions once the
  control-protocol baseline (Phase 6) lands.

## Dependency graph

```
Phase 1: repo skeleton ──┐
                         ├──→ Phase 3: wire types & JSON ──┐
Phase 2: native bundle ──┘                                 │
                                                           ├──→ Phase 4: transport/process plumbing
                                                           │             │
                                                           │             ├──→ Phase 5: one-shot QueryAsync ◄── FIRST MILESTONE
                                                           │             │             │
                                                           │             │             ├──→ Phase 6: streaming + control-protocol baseline
                                                           │             │             │             │
                                                           │             │             │             ├──→ Phase 7: hooks            ┐
                                                           │             │             │             ├──→ Phase 8: permissions      │ parallelizable
                                                           │             │             │             ├──→ Phase 9: sessions         │ once Phase 6
                                                           │             │             │             ├──→ Phase 10–11: MCP package  │ lands
                                                           │             │             │             └──→ Phase 13: testing helpers ┘
                                                           │             │             │
                                                           │             │             └──→ Phase 12: DI package (needs streaming client)
                                                           │             │
                                                           │             └──→ Phase 14: samples (need every feature slice)
                                                           │
                                                           └──→ Phase 15: CI matrix, release pipeline, sign + SBOM
                                                                         │
                                                                         └──→ Phase 16: README + migration-from-Python guide
```

## Task sizing

Most tasks are **S** (1–2 files) or **M** (3–5 files). Anything that reads as
**L** in this document is a *bag of S/M tasks* — it stays at one bullet here
because the sub-tasks are mechanical/repetitive (e.g. "13 hook input record
types"). When work begins, Phase 6+ consumers should split these mechanical
bags using the same template.

---

## Phase 1: Repository foundation

Goal: empty solution that builds and runs `dotnet test` (with zero tests) on
all three CI OSes.

### Task 1.1: Create solution skeleton — S
**Description:** Initialize git repo, `claude-agent-sdk-dotnet.sln`, `LICENSE`
(MIT), placeholder `README.md`, `CHANGELOG.md`, `RELEASING.md`, `.gitignore`,
`nuget.config`.
**Acceptance:**
- [x] `dotnet --version` resolves the .NET 10 SDK band via `global.json`.
- [x] `dotnet build` on the empty `.sln` succeeds.
**Verification:** `dotnet build && dotnet test` exits 0. ✅ Done in 6c4153e.
Note: .NET 10's `dotnet new sln` defaults to `.slnx`, so the solution
is `Daystrom.ClaudeAgentSdk.slnx`, not `.sln`.
**Dependencies:** None.
**Files:** `Daystrom.ClaudeAgentSdk.slnx`, `global.json`, `LICENSE`, `README.md`,
`CHANGELOG.md`, `RELEASING.md`, `.gitignore`, `nuget.config`.

### Task 1.2: Central build configuration — S
**Description:** `Directory.Build.props` enforcing `Nullable=enable`,
`TreatWarningsAsErrors=true`, `LangVersion=14`, `IsAotCompatible=true` (core
only via condition), `Deterministic=true`, `ContinuousIntegrationBuild` from
env var. `Directory.Packages.props` for Central Package Management with the
spec's pinned versions (STJ, MEL.Abstractions, ModelContextProtocol,
MEDI.Abstractions, MEO.ConfigurationExtensions, Microsoft.Sbom.Targets,
xUnit, Verify.Xunit). `eng/Versions.props` carrying `<ClaudeCliVersion>` and
`<SdkVersion>`.
**Acceptance:**
- [x] All package versions live in `Directory.Packages.props`; no version
  literals appear in any `.csproj`. (Trivially true in Phase 1 — no csprojs
  yet. The `<PackageVersion>` list is intentionally empty; entries land
  per-phase as packages are first consumed.)
- [x] Adding a new project picks up Nullable + WarningsAsErrors automatically.
**Verification:** `dotnet restore --locked-mode` succeeds. ✅ Done in 2ec8356.
Verified via throwaway `eng/.smoke/Smoke.csproj`: deliberate nullable
violations were promoted to CS8600/CS8603 errors, and `msbuild
-getProperty` round-tripped `ClaudeCliVersion`, `SdkVersion`, `Nullable`,
`TreatWarningsAsErrors`, `LangVersion`. CPM enforcement deferred until
Phase 3 (first real PackageReference).
**Dependencies:** 1.1.
**Files:** `Directory.Build.props`, `Directory.Packages.props`, `eng/Versions.props`.

### Task 1.3: Empty CI workflow — S
**Description:** `.github/workflows/ci.yml` with matrix (`ubuntu-latest`,
`windows-latest`, `macos-latest`), running `dotnet restore`, `dotnet build`,
`dotnet test`. No code coverage yet (added in Phase 15).
**Acceptance:**
- [x] Push to a feature branch triggers all three matrix legs and they pass.
  *(Authored in 07d7f49; verified once remote was added — CI green on Linux/macOS/Windows.)*
**Verification:** Green check on a draft PR.
**Dependencies:** 1.2.
**Files:** `.github/workflows/ci.yml`.

### Task 1.4 (added): CSharpier integration
Not in the original spec; added on user request after Phase 1's three
core tasks. Pinned CSharpier 1.2.6 as a local tool in
`dotnet-tools.json` (root, per .NET 10's new default location), and
added a Linux-only `format` job to `ci.yml` that runs
`dotnet csharpier check .`. Existing files reformatted in place.
**Acceptance:**
- [x] `dotnet csharpier check .` passes locally.
- [x] CI workflow gates merges on format compliance.
**Verification:** ✅ Done in d16ff6c.

### Checkpoint: Phase 1
- [x] Empty solution builds and tests on Linux, macOS, Windows in CI.
  *(Verified across the matrix once remote was wired up.)*
- [x] Central Package Management + Nullable + WarningsAsErrors verified by
  attempting to add a project that omits each — build fails as expected.
  *(Nullable + WoE fully verified via smoke csproj; CPM enforcement
  deferred to Phase 3 when the first `PackageReference` lands.)*

---

## Phase 2: Native bundle pipeline

Goal: five RID-specific NuGet packages that pack the pinned `claude` Node CLI.
This phase has **zero dependency on SDK code** and can run in parallel with
Phases 3–4.

### Task 2.1: CLI download script — M
**Description:** `eng/download-claude-cli.ps1` (cross-platform PowerShell):
`npm pack @anthropic-ai/claude-code@$(ClaudeCliVersion)` per RID, extract,
drop binary into the matching `runtimes/{rid}/native/` folder. Idempotent;
fails loudly if upstream tarball is missing.
**Acceptance:**
- [x] Run on Linux: drops `claude` into `linux-x64` and `linux-arm64` payload
  folders.
- [x] Run on macOS: drops `claude` into `osx-x64` and `osx-arm64`.
- [x] Run on Windows: drops `claude.exe` into `win-x64`.
- [x] Re-running with the cache populated is a no-op.
**Verification:** Hash of dropped binary matches the upstream npm tarball's
declared SHA.
**Dependencies:** 1.2.
**Files:** `eng/download-claude-cli.ps1`.

### Task 2.2: Five RID native csproj projects — S × 5
**Description:** One csproj per RID under
`src/runtimes/runtime.{rid}.Daystrom.ClaudeAgentSdk.Native/`. Each is
`<IncludeBuildOutput>false</IncludeBuildOutput>`, packs only
`runtimes/{rid}/native/**` payload, no compile sources.
**Acceptance:**
- [x] `dotnet pack` on each produces a `.nupkg` whose contents are *only* the
  binary under `runtimes/{rid}/native/`.
- [x] Package size is within the budget noted in `RELEASING.md`.
**Verification:** `nuget verify` and a manual unzip check on each `.nupkg`.
**Dependencies:** 2.1.
**Files:** 5 × `runtime.{rid}.Daystrom.ClaudeAgentSdk.Native.csproj`.

### Task 2.3: Native bundle workflow — S
**Description:** `.github/workflows/native-bundle.yml` triggered on `v*` tags
and `workflow_dispatch`. Runs `download-claude-cli.ps1`, packs five sub-
packages, attaches as artifacts.
**Acceptance:**
- [x] Manual dispatch produces five artifacts on the run summary page.
**Verification:** Artifacts can be downloaded and installed via `dotnet add
package` from a local feed.
**Dependencies:** 2.2.
**Files:** `.github/workflows/native-bundle.yml`, `eng/pack-native.ps1`.

### Checkpoint: Phase 2
- [x] All five RID packages build, pack, and contain only the binary payload.
- [x] CI tag-trigger smoke-tested on a throwaway tag.

---

## Phase 3: Wire types & JSON contract

Goal: every type that crosses the wire (CLI ⇄ SDK) exists, is serializable via
the source-gen context, and round-trips through saved Python fixtures.

### Task 3.1: Errors hierarchy — S
**Description:** `Errors/ClaudeSdkException.cs` base; `CliConnectionException`,
`CliNotFoundException`, `ProcessException` (with `ExitCode`, `Stderr`),
`CliJsonDecodeException` (with `RawLine`).
**Acceptance:**
- [x] All five types compile, are `sealed` where appropriate, have XML doc
  comments.
**Verification:** Smoke unit test instantiates each and serializes its
`.Message`/`.ToString()`. ✅ Done in 7739696.
**Dependencies:** 1.2.
**Files:** `src/Daystrom.ClaudeAgentSdk/Errors/*.cs`.

### Task 3.2: Enums and small DTOs — S
**Description:** `PermissionMode`, `SettingSource`, `SdkBeta`,
`HookEvent`, `ServerToolName`, `RateLimitType`, `ContextUsageCategory`,
`McpServerStatusConfig`, `McpServerConnectionStatus`, `SessionStoreFlushMode`.
**Acceptance:**
- [x] Each enum has a `[JsonStringEnumConverter]` (snake-case lower) attribute
  on the property, *not* the type, so STJ source-gen stays happy.
**Verification:** Round-trip enum → JSON → enum for every member. ✅ Done in 7739696.
**Dependencies:** 3.1.
**Files:** `src/Daystrom.ClaudeAgentSdk/{PermissionMode,SettingSource,...}.cs`.

### Task 3.3: Content blocks discriminated union — M
**Description:** `Messages/Content/ContentBlock.cs` (abstract record) +
variants: `TextBlock`, `ThinkingBlock`, `ToolUseBlock`, `ToolResultBlock`,
`ServerToolUseBlock`, `ServerToolResultBlock`. `[JsonPolymorphic]` with
`type` discriminator.
**Acceptance:**
- [x] All six variants round-trip through Python-fixture JSON.
- [x] Unknown discriminator throws `CliJsonDecodeException`, not
  `JsonException`.
**Verification:** `MessageParserTests.RoundTripsAllPythonFixtures` passes for
content-block fixtures. ✅ Done in 7739696.
**Dependencies:** 3.1, 3.2.
**Files:** `src/Daystrom.ClaudeAgentSdk/Messages/Content/*.cs`.

### Task 3.4: Top-level Message variants — M
**Description:** `Messages/Message.cs` abstract + `AssistantMessage`,
`UserMessage`, `SystemMessage`, `ResultMessage`, `StreamEvent`,
`TaskStartedMessage`, `TaskProgressMessage`, `TaskNotificationMessage`. Plus
helper records: `RateLimitInfo`, `RateLimitEvent`, `RateLimitStatus`,
`ContextUsageResponse`, `TaskUsage`, `TaskBudget`.
**Acceptance:**
- [x] All variants round-trip through fixtures.
- [x] `ResultMessage` shape is byte-identical between one-shot and streaming
  fixtures (this is what makes Phase 5's fast path safe).
**Verification:** `MessageParserTests` for every variant. ✅ Done in 7739696.
**Dependencies:** 3.3.
**Files:** `src/Daystrom.ClaudeAgentSdk/Messages/*.cs`.

### Task 3.5: Hook input/output records — M (bag of mechanical S tasks)
**Description:** `Hooks/HookContext.cs`, `HookMatcher.cs`, `HookResult.cs`
(abstract + `Continue`/`Block`/`Modify`), `HookJsonOutput.cs`. All 10
`HookInput` variants under `Hooks/Inputs/`. All 4 specific-output types
under `Hooks/Outputs/`. `IHookHandler<TInput>`, `HookHandler<TInput>` and
non-generic `HookHandler` delegates.
**Acceptance:**
- [x] Each hook input round-trips through Python fixture.
- [x] The non-generic erased `HookHandler` delegate compiles and accepts a
  closure over a strongly-typed `HookHandler<TInput>` (proves the wrapping
  story works).
**Verification:** `HookSerializationTests` per variant. ✅ Done in 7739696.
**Dependencies:** 3.3, 3.4.
**Files:** `src/Daystrom.ClaudeAgentSdk/Hooks/**/*.cs`.

### Task 3.6: Permissions, MCP, Sandbox, Thinking, Agents, Plugins types — M
**Description:** All remaining DTOs from spec §7 that are *not* surface API:
`PermissionResult`, `PermissionUpdate`, `ToolPermissionContext`,
`McpServerConfig` + 4 variants, `IMcpServerInstance`, `McpServerStatus`,
`McpServerInfo`, `McpStatusResponse`, `McpToolInfo`, `McpToolAnnotations`,
`SandboxSettings`, `SandboxNetworkConfig`, `SandboxIgnoreViolations`,
`ThinkingConfig` + 3 variants, `AgentDefinition`, `SdkPluginConfig`.
**Acceptance:**
- [x] All discriminated unions (`McpServerConfig`, `ThinkingConfig`,
  `PermissionResult`) parse Python-fixture JSON.
- [x] `SandboxSettings` XML doc comment explicitly calls out the native-
  Windows no-op caveat (spec §7).
**Verification:** Round-trip tests per type. ✅ Done in 7739696.
**Dependencies:** 3.3, 3.4, 3.5.
**Files:** `src/Daystrom.ClaudeAgentSdk/{Permissions,Mcp,Sandbox,ThinkingConfig,Agents}/*.cs`.

### Task 3.7: Session DTOs — S
**Description:** `Sessions/{SessionKey,SessionStoreEntry,SessionStoreListEntry,
SessionListSubkeysKey,SessionSummaryEntry,SessionMessage,SDKSessionInfo,
ForkSessionResult,MirrorErrorMessage}.cs`. No client/store *behavior* yet —
just the records.
**Acceptance:**
- [x] All round-trip through fixture JSON.
**Verification:** Per-type round-trip tests. ✅ Done in 7739696.
**Dependencies:** 3.4.
**Files:** `src/Daystrom.ClaudeAgentSdk/Sessions/*.cs` (DTOs only).

### Task 3.8: Source-gen JSON context — M
**Description:** `Json/ClaudeAgentJsonContext.cs` declares `[JsonSerializable]`
for every wire type from 3.1–3.7. Snake-case lower naming policy on the
context options. Properties whose snake-case form collides with a C# keyword
get `[JsonPropertyName]`.
**Acceptance:**
- [x] `dotnet build /p:IsTrimmed=true /p:PublishAot=true` on the core
  project produces zero trim and zero AOT warnings.
- [x] No reflection-based STJ call site exists in the core project (verified
  by an analyzer rule or grep in the test suite).
**Verification:** Trim/AOT publish in CI succeeds with `--verbosity normal`
showing no `IL2*` or `IL3*` warnings. ✅ Done in 7739696; direct context tests added in 6f3a1b7.
**Dependencies:** 3.1–3.7.
**Files:** `src/Daystrom.ClaudeAgentSdk/Json/ClaudeAgentJsonContext.cs`.

### Checkpoint: Phase 3
- [x] Every wire type compiles, has XML doc comments, and round-trips through
  at least one Python-fixture sample.
- [x] Core project is `IsAotCompatible=true` with zero warnings.

---

## Phase 4: Transport & process plumbing

Goal: spawn the bundled CLI, ship NDJSON over stdio, shut down cleanly.

### Task 4.1: `CliBinaryResolver` — M
**Description:** Implements the spec §10 lookup order: `options.CliPath` →
`AppContext.BaseDirectory/runtimes/{rid}/native/claude{,.exe}` → `PATH` →
the six fallback paths. `ILogger` warning on native Windows when
`Sandbox` is non-null (spec §7 caveat). CLI version check via `claude -v`,
gated on `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK`.
**Acceptance:**
- [ ] Each lookup step is unit-testable with an injected filesystem
  abstraction.
- [ ] Throws `CliNotFoundException` with all attempted paths in the message
  when nothing resolves.
**Verification:** `CliBinaryResolverTests` covers each branch.
**Dependencies:** 3.1, 3.6 (Sandbox).
**Files:** `src/Daystrom.ClaudeAgentSdk/Transport/CliBinaryResolver.cs`.

### Task 4.2: `NdjsonReader` with speculative buffering — M
**Description:** Reads from a `Stream`, yields `JsonElement` per line.
Configurable max buffer (default 1 MiB, from `Options.MaxBufferSize`).
Tolerates `TextReceiveStream`-style line truncation by buffering until a
parse succeeds or max-buffer trips. Throws `CliJsonDecodeException` (with
raw line) on permanent parse failure.
**Acceptance:**
- [ ] Round-trips a stream where lines are split arbitrarily across reads.
- [ ] Max-buffer overflow throws with the partial buffer in the exception.
**Verification:** `NdjsonReaderTests` with adversarial chunking.
**Dependencies:** 3.1.
**Files:** `src/Daystrom.ClaudeAgentSdk/Transport/NdjsonReader.cs`.

### Task 4.3: `ProcessGracefulShutdown` — S
**Description:** Helper encapsulating the spec §9 shutdown sequence:
close stdin → `WaitForExitAsync(5s)` → `Process.Kill(false)` →
`WaitForExitAsync(5s)` → `Process.Kill(true)`. Idempotent.
**Acceptance:**
- [ ] Test with a process that exits immediately on stdin EOF: never reaches
  `Kill`.
- [ ] Test with a process that ignores stdin EOF: reaches `Kill(false)`.
- [ ] Test with a process that ignores SIGTERM: reaches `Kill(true)`.
**Verification:** `ProcessGracefulShutdownTests` using a fixture binary
(can be a `dotnet run` of a tiny helper).
**Dependencies:** 3.1.
**Files:** `src/Daystrom.ClaudeAgentSdk/Internal/ProcessGracefulShutdown.cs`.

### Task 4.4: `OtelContextInjector` — S
**Description:** Reads `Activity.Current`, injects `traceparent`/`tracestate`
into the env-var dictionary destined for the child process via
`DistributedContextPropagator.Current`. Scrubs inherited env vars when a
fresh `Activity` is active *unless* `Options.Env` explicitly sets them.
**Acceptance:**
- [ ] When no `Activity` is active, env passthrough is unchanged.
- [ ] When an `Activity` is active, child env contains the *current* span's
  `traceparent`, not the parent process's.
**Verification:** `OtelContextInjectorTests`.
**Dependencies:** 3.1.
**Files:** `src/Daystrom.ClaudeAgentSdk/Internal/OtelContextInjector.cs`.

### Task 4.5: `ClaudeAgentOptions` record + builder — M
**Description:** The full record from spec §8 + the fluent builder. No CLI
flag emission yet (that's 4.6). Just the immutable shape and the builder
that produces it.
**Acceptance:**
- [ ] `Builder.Build()` produces an instance whose every field round-trips
  through `with` expressions.
- [ ] Every `On*` builder method is covered by a builder test.
**Verification:** `ClaudeAgentOptionsBuilderTests`.
**Dependencies:** 3.5, 3.6.
**Files:** `src/Daystrom.ClaudeAgentSdk/ClaudeAgentOptions.cs`,
`ClaudeAgentOptionsBuilder.cs`.

### Task 4.6: `CommandBuilder` — M (bag of S tasks per flag)
**Description:** `Internal/CommandBuilder.cs` mirrors Python's
`_build_command()` exactly. Every flag in spec §9.1. Two output modes:
streaming (appends `--input-format stream-json`) and one-shot (appends
`--print -- "<prompt>"`). Skills defaults applied identically to Python.
Sandbox merged into `--settings` JSON.
**Acceptance:**
- [ ] Snapshot tests (`Verify.Xunit`) for ~30 representative option
  permutations covering every flag at least once.
- [ ] Argv exactly matches Python's emission for the same options (use saved
  Python output as the snapshot baseline).
**Verification:** `CommandBuilderTests`. Snapshots checked into the repo.
**Dependencies:** 4.5.
**Files:** `src/Daystrom.ClaudeAgentSdk/Internal/CommandBuilder.cs`.

### Task 4.7: `ITransport` interface + `SubprocessCliTransport` (one-shot capable) — M
**Description:** `ITransport` public extension point. Default
`SubprocessCliTransport` that spawns the CLI via `CliBinaryResolver`,
applies env munging (`CLAUDE_CODE_ENTRYPOINT=sdk-dotnet`,
`CLAUDE_AGENT_SDK_VERSION=<v>`, filter `CLAUDECODE`), wires stdin/stdout
to `NdjsonReader` + an async writer with `_writeLock`, and uses
`ProcessGracefulShutdown` on dispose. **One-shot mode only this phase** —
no control-protocol initialize handshake.
**Acceptance:**
- [ ] Spawning a fixture binary that emits 5 NDJSON lines reads them all.
- [ ] Cancellation closes stdin and triggers shutdown.
- [ ] Stderr is forwarded to `Options.Stderr` callback only when set
  (otherwise the pipe is left at default).
**Verification:** `SubprocessCliTransportTests` using a fixture script.
**Dependencies:** 4.1, 4.2, 4.3, 4.4, 4.6.
**Files:** `src/Daystrom.ClaudeAgentSdk/Transport/{ITransport,SubprocessCliTransport}.cs`.

### Checkpoint: Phase 4
- [ ] Plumbing components individually unit-tested.
- [ ] An ad-hoc `dotnet run` against the bundled CLI streams `stream-json`
  output to stdout. (Phase 5 turns this into the real public API.)

---

## Phase 5: One-shot QueryAsync — FIRST USER-FACING MILESTONE

Goal: `await foreach (var m in ClaudeAgent.QueryAsync("hello", options)) ...`
returns real `Message` records from a real `claude` subprocess. No control
protocol, no hooks, no permissions, no MCP.

### Task 5.1: `MessageParser` — S
**Description:** Wraps `JsonSerializer.Deserialize` over the source-gen
context. Translates `JsonException` → `CliJsonDecodeException` with the raw
line attached.
**Acceptance:**
- [ ] All Python fixtures parse without error.
- [ ] A malformed line yields `CliJsonDecodeException` whose `RawLine` is the
  exact byte sequence read.
**Verification:** `MessageParserTests`.
**Dependencies:** 3.8, 4.2.
**Files:** `src/Daystrom.ClaudeAgentSdk/Internal/MessageParser.cs`.

### Task 5.2: `ControlProtocolGate` — XS
**Description:** `Internal/ControlProtocolGate.cs` with `bool
NeedsControlProtocol(ClaudeAgentOptions, ITransport?)` returning true iff:
`CanUseTool != null` || `Hooks?.Count > 0` || any `McpSdkServerConfig` in
`McpServers` || a custom `ITransport` was supplied.
**Acceptance:**
- [ ] Truth table covered: 4 positive cases × 1 negative.
**Verification:** `ControlProtocolGateTests`.
**Dependencies:** 4.5.
**Files:** `src/Daystrom.ClaudeAgentSdk/Internal/ControlProtocolGate.cs`.

### Task 5.3: `ClaudeAgent.QueryAsync(string, ...)` one-shot path — M
**Description:** Static method. If `ControlProtocolGate.NeedsControlProtocol`
returns false: build argv with `--print -- "<prompt>"`, spawn via
`SubprocessCliTransport`, close stdin immediately, read NDJSON until EOF,
yield parsed `Message` records. Cancellation cancels the read and shuts the
process down.
**Acceptance:**
- [ ] End-to-end against bundled CLI: `await foreach (var m in
  ClaudeAgent.QueryAsync("ping"))` yields at least an `AssistantMessage` and
  a `ResultMessage`.
- [ ] Cancelling the token mid-stream terminates the process within 5 s.
- [ ] No control-protocol traffic appears on stdin (verified by
  `RecordingTransport`).
**Verification:** `QueryOneShotTests` (unit, with fake transport) +
`QueryOneShotIntegrationTests` (gated `CLAUDE_INTEGRATION=1`).
**Dependencies:** 5.1, 5.2, 4.7.
**Files:** `src/Daystrom.ClaudeAgentSdk/ClaudeAgent.cs`.

### Task 5.4: `ClaudeAgent.SdkVersion` constant — XS
**Description:** Public string property exposing the SDK's assembly version,
also written into `CLAUDE_AGENT_SDK_VERSION` env var by the transport.
**Acceptance:**
- [ ] Matches the version in `eng/Versions.props`.
**Verification:** `SdkVersionTests`.
**Dependencies:** 5.3.
**Files:** `src/Daystrom.ClaudeAgentSdk/ClaudeAgent.cs` (partial).

### Checkpoint: Phase 5 — first shippable demo
- [ ] `samples/QuickStart` (stub) prints a real assistant response.
- [ ] Integration test runs against `ANTHROPIC_API_KEY` in CI Linux.
- [ ] Trim/AOT publish of the QuickStart sample succeeds with zero warnings.
- [ ] **Decision point with human:** review the public API surface from
  Phase 5 before layering control protocol on top.

---

## Phase 6: Streaming client + control-protocol baseline

Goal: `IClaudeAgentClient` works for multi-turn streaming, including
`InterruptAsync` and the `initialize` handshake. No hook/permission/MCP
*handlers* yet — just the protocol scaffolding.

### Task 6.1: `ControlMessages` types — S
**Description:** All control-request and control-response shapes:
`initialize`, `interrupt`, `set_permission_mode`, `mcp_message`,
hook callback envelope, `can_use_tool`, `control_cancel_request`. Source-gen
serializable.
**Acceptance:**
- [ ] Each type round-trips through fixture JSON.
**Verification:** `ControlMessagesTests`.
**Dependencies:** 3.8.
**Files:** `src/Daystrom.ClaudeAgentSdk/Control/ControlMessages.cs`.

### Task 6.2: `ControlProtocol` core — M
**Description:** `Control/ControlProtocol.cs` — owns the request/response
correlation table (request_id → `TaskCompletionSource`), routes inbound
control messages by subtype to registered handlers, exposes
`SendRequestAsync<TReq, TResp>`. **Receives `control_cancel_request` and
discards** (spec §13 v1 behavior). XML doc on the cancel-handling method
spells out the v1 semantics so callers aren't surprised.
**Acceptance:**
- [ ] `initialize` round-trip via `FakeTransport` succeeds.
- [ ] Concurrent outbound requests don't interleave (write lock honored).
- [ ] Inbound `control_cancel_request` is logged at debug level and
  discarded; in-flight callback still completes.
**Verification:** `ControlProtocolTests` with scripted FakeTransport.
**Dependencies:** 6.1, 4.7.
**Files:** `src/Daystrom.ClaudeAgentSdk/Control/ControlProtocol.cs`.

### Task 6.3: `SubprocessCliTransport` streaming-mode upgrade — S
**Description:** Add streaming mode to the transport: don't append `--print`,
keep stdin open, run the writer task, ack `initialize`. Flag-flip based on
constructor parameter from `ClaudeAgentClient`.
**Acceptance:**
- [ ] Streaming-mode transport survives a 1000-line input/output exchange
  without deadlock.
**Verification:** `SubprocessCliTransportStreamingTests`.
**Dependencies:** 4.7, 6.2.
**Files:** `src/Daystrom.ClaudeAgentSdk/Transport/SubprocessCliTransport.cs`.

### Task 6.4: `ClaudeAgentClient` — M
**Description:** Implements `IClaudeAgentClient`: `ConnectAsync` (spawn +
initialize handshake), `SendUserMessageAsync` (writes user input as NDJSON),
`ReceiveMessagesAsync` (yields parsed messages until disconnect),
`EndInputAsync` (closes stdin), `DisconnectAsync` / `DisposeAsync` (graceful
shutdown), `InterruptAsync` (sends `control_request{interrupt}` and awaits
ack), `GetMcpStatusAsync`, `GetContextUsageAsync`,
`ApplyPermissionUpdateAsync`. Static `Create(options, transport?)` factory.
**Acceptance:**
- [ ] Multi-turn conversation against bundled CLI: send 3 user turns, receive
  3 assistant responses.
- [ ] `InterruptAsync` mid-turn cancels the assistant's response.
- [ ] `await using` scope cleanly shuts down the process.
**Verification:** `ClaudeAgentClientTests` + integration test.
**Dependencies:** 6.2, 6.3, 5.1.
**Files:** `src/Daystrom.ClaudeAgentSdk/{IClaudeAgentClient,ClaudeAgentClient}.cs`.

### Task 6.5: `ClaudeAgent.QueryAsync(IAsyncEnumerable<UserMessageInput>, ...)` — S
**Description:** The streaming-input overload. Always uses streaming path
regardless of `ControlProtocolGate`. Wraps `ClaudeAgentClient` internally.
**Acceptance:**
- [ ] Streaming-input fixture produces interleaved messages.
**Verification:** `QueryStreamingInputTests`.
**Dependencies:** 6.4.
**Files:** `src/Daystrom.ClaudeAgentSdk/ClaudeAgent.cs` (partial).

### Checkpoint: Phase 6
- [ ] Streaming client demo (`samples/StreamingMode` stub) works.
- [ ] One-shot path from Phase 5 still works unchanged.
- [ ] No control-protocol regression in the one-shot path (verified by
  `RecordingTransport` snapshot).

---

## Phase 7: Hooks vertical slice

Goal: every hook event from `HookEvent` enum can be registered via the
fluent builder and is routed to user code through the control protocol.

### Task 7.1: Hook callback routing through `ControlProtocol` — M
**Description:** Register hook handlers (keyed by `HookEvent` + matcher) on
the `ControlProtocol`. On inbound hook callback control-request, look up the
matching handler chain, invoke each, merge `HookResult`s per Python
semantics, send the response. Errors in user handlers become `HookResult.Block`
with the exception message and are logged at warn level.
**Acceptance:**
- [ ] Each of the 10 hook events routes correctly.
- [ ] `Continue` / `Block` / `Modify` results serialize to the wire shape the
  CLI expects.
- [ ] User handler exception → `Block` response, no process crash.
**Verification:** `HookRoutingTests` with FakeTransport scripts per event.
**Dependencies:** 6.2, 3.5.
**Files:** `src/Daystrom.ClaudeAgentSdk/Control/ControlProtocol.cs` (partial).

### Task 7.2: Builder `On*` methods — M (bag of mechanical S tasks)
**Description:** One `On<Event>` method on `ClaudeAgentOptionsBuilder` per
hook event, taking `(string matcher, HookHandler<TInput> handler)`. Wraps
the strongly-typed handler in the erased `HookHandler` form for storage.
**Acceptance:**
- [ ] Each `On*` method has a builder test that registers a handler and
  asserts it appears in the built options.
**Verification:** `ClaudeAgentOptionsBuilderHookTests`.
**Dependencies:** 7.1, 4.5.
**Files:** `src/Daystrom.ClaudeAgentSdk/ClaudeAgentOptionsBuilder.cs` (partial).

### Checkpoint: Phase 7
- [ ] `samples/Hooks` stub registers `PreToolUse` + `PostToolUse` and prints
  observed tool calls.
- [ ] Integration test asserts a Block result from `PreToolUse` actually
  blocks the tool call downstream.

---

## Phase 8: Permissions / can_use_tool slice

Goal: `Options.CanUseTool` callback is invoked by the CLI, and
`PermissionUpdate` can be pushed from `IClaudeAgentClient`.

### Task 8.1: `can_use_tool` routing — S
**Description:** On inbound `can_use_tool` control-request, invoke the
registered `CanUseToolDelegate`, serialize `Allow`/`Deny` (with optional
`UpdatedInput` / `Reason` / `Interrupt`) as the response.
**Acceptance:**
- [ ] FakeTransport scripts both Allow and Deny and asserts wire response.
- [ ] If no callback registered, sends a reasonable default
  (matches Python — re-check spec).
**Verification:** `CanUseToolTests`.
**Dependencies:** 6.2.
**Files:** `src/Daystrom.ClaudeAgentSdk/Control/ControlProtocol.cs` (partial).

### Task 8.2: `ApplyPermissionUpdateAsync` outbound — XS
**Description:** Wire the `IClaudeAgentClient` method through the
`ControlProtocol` `set_permission_mode` request shape.
**Acceptance:**
- [ ] FakeTransport sees the correct request shape.
**Verification:** `PermissionUpdateTests`.
**Dependencies:** 6.4.
**Files:** `src/Daystrom.ClaudeAgentSdk/ClaudeAgentClient.cs` (partial).

### Checkpoint: Phase 8
- [ ] `samples/ToolPermissionCallback` stub denies `Bash` for a specific
  command pattern.

---

## Phase 9: Sessions slice

Goal: every session API listed in spec §19 row works, including the optional
`ISessionStore` plug-point.

### Task 9.1: `ISessionStore` + `InMemorySessionStore` — S
**Description:** Pluggable session backend interface. In-memory reference
implementation.
**Acceptance:**
- [ ] CRUD round-trip on the in-memory store.
- [ ] Flush mode honored.
**Verification:** `InMemorySessionStoreTests`.
**Dependencies:** 3.7.
**Files:** `src/Daystrom.ClaudeAgentSdk/Sessions/{ISessionStore,InMemorySessionStore}.cs`.

### Task 9.2: `SessionsClient` — M
**Description:** Static API: `ListSessionsAsync`, `GetSessionInfoAsync`,
`GetSessionMessagesAsync`, `ListSubagentsAsync`, `GetSubagentMessagesAsync`,
`ListSessionsFromStoreAsync` + `*ViaStoreAsync` variants,
`RenameSessionAsync`, `TagSessionAsync`, `DeleteSessionAsync`,
`ForkSessionAsync`. All return strongly-typed records.
**Acceptance:**
- [ ] Each method's wire request matches Python's emission for the same call.
- [ ] FakeTransport tests cover happy path + one error path per method.
**Verification:** `SessionsClientTests`.
**Dependencies:** 9.1, 6.2.
**Files:** `src/Daystrom.ClaudeAgentSdk/Sessions/SessionsClient.cs`.

### Task 9.3: `SessionImporter` + `SessionSummaryFolder` + `project_key_for_directory` — S
**Description:** The standalone helper functions from spec §19.
**Acceptance:**
- [ ] Per-function unit tests against fixture data.
**Verification:** `SessionHelperTests`.
**Dependencies:** 9.1.
**Files:** `src/Daystrom.ClaudeAgentSdk/Sessions/{SessionImporter,SessionSummaryFolder}.cs`.

### Checkpoint: Phase 9
- [ ] `samples/SessionResume` stub forks a session and resumes from it.

---

## Phase 10: MCP package — attribute path

Goal: `samples/McpCalculator`-style usage — decorate a class with
`[McpServerToolType]` and pass it via `SdkMcpServer.FromType<T>`.

### Task 10.1: `Daystrom.ClaudeAgentSdk.Mcp` project skeleton — S
**Description:** New csproj depending on core +
`ModelContextProtocol`. `IsAotCompatible=true` (attribute path uses MCP SDK's
source generator).
**Acceptance:**
- [ ] `dotnet pack` produces a valid `.nupkg`.
**Verification:** Empty smoke test.
**Dependencies:** 1.2.
**Files:** `src/Daystrom.ClaudeAgentSdk.Mcp/Daystrom.ClaudeAgentSdk.Mcp.csproj`.

### Task 10.2: `SdkMcpServer.FromType` — M
**Description:** Static helper that takes a `[McpServerToolType]`-decorated
class, instantiates the MCP SDK's `IMcpServer`, wraps it in
`IMcpServerInstance`, and returns an `McpSdkServerConfig` ready to drop into
`Options.McpServers`.
**Acceptance:**
- [ ] A trivial calculator class with `[McpServerTool]` methods is exposed
  end-to-end via `FromType<Calculator>`.
- [ ] `FromType` overload taking `Type` works the same.
**Verification:** `SdkMcpServerFromTypeTests`.
**Dependencies:** 10.1, 3.6.
**Files:** `src/Daystrom.ClaudeAgentSdk.Mcp/SdkMcpServer.cs`.

### Task 10.3: `mcp_message` routing in `ControlProtocol` — M
**Description:** When the CLI sends `control_request{mcp_message}` for a
registered `McpSdkServerConfig`, dispatch to the wrapped `IMcpServerInstance`,
serialize the result back. Honors cancellation per spec §13.
**Acceptance:**
- [ ] FakeTransport simulates an `mcp_message` for a calculator tool;
  response matches expected shape.
- [ ] Errors in the tool become a JSON-RPC error response, not a process
  crash.
**Verification:** `McpMessageRoutingTests`.
**Dependencies:** 10.2, 6.2.
**Files:** `src/Daystrom.ClaudeAgentSdk/Control/ControlProtocol.cs` (partial).

### Checkpoint: Phase 10
- [ ] `samples/McpCalculator` stub computes `2+2` via the in-process MCP path.

---

## Phase 11: MCP package — fluent path

Goal: `SdkMcpServer.Create("name").AddTool(...).Build()` works for users who
don't want attribute decoration.

### Task 11.1: `SdkMcpServerBuilder` strongly-typed `AddTool<TIn, TOut>` — S
**Description:** Generic, AOT-friendly tool registration.
**Acceptance:**
- [ ] Equivalent of the calculator sample built via fluent API works
  end-to-end.
**Verification:** `SdkMcpServerBuilderTests`.
**Dependencies:** 10.2.
**Files:** `src/Daystrom.ClaudeAgentSdk.Mcp/SdkMcpServerBuilder.cs`.

### Task 11.2: Reflection-based `AddTool(Delegate)` overload — S
**Description:** Convenience overload annotated `[RequiresUnreferencedCode]`
+ `[RequiresDynamicCode]`. Documented as not-AOT-safe in XML.
**Acceptance:**
- [ ] Compiles with the annotations producing the expected analyzer warning
  in a trim-enabled consumer project.
**Verification:** `SdkMcpServerBuilderReflectionTests` (xUnit conditional on
non-AOT build).
**Dependencies:** 11.1.
**Files:** `src/Daystrom.ClaudeAgentSdk.Mcp/SdkMcpServerBuilder.cs` (partial).

### Checkpoint: Phase 11
- [ ] Fluent-API variant of the calculator sample works.

---

## Phase 12: DI package

Goal: `services.AddClaudeAgent(configure)` plus
`AddClaudeAgentHook<T>()`.

### Task 12.1: `Daystrom.ClaudeAgentSdk.DependencyInjection` skeleton — S
**Description:** Csproj depending on core + MEDI.Abstractions +
MEO.ConfigurationExtensions.
**Acceptance:**
- [ ] `dotnet pack` produces a valid `.nupkg`.
**Verification:** Empty smoke test.
**Dependencies:** 1.2.
**Files:** `src/Daystrom.ClaudeAgentSdk.DependencyInjection/*.csproj`.

### Task 12.2: `AddClaudeAgent` overloads — M
**Description:** Three overloads from spec §8: parameterless,
`Action<ClaudeAgentOptions>`, `IConfiguration`. Registers
`IClaudeAgentClient` as scoped, a `ClaudeAgentFactory` as singleton (for
one-shot enumerables).
**Acceptance:**
- [ ] DI test asserts both registrations resolve correctly.
- [ ] `IConfiguration` binding round-trips a settings JSON file.
**Verification:** `AddClaudeAgentTests`.
**Dependencies:** 12.1, 6.4.
**Files:** `src/Daystrom.ClaudeAgentSdk.DependencyInjection/ClaudeAgentServiceCollectionExtensions.cs`.

### Task 12.3: `AddClaudeAgentHook<T>` — S
**Description:** Convenience that registers a class-based `IHookHandler<T>`
and wires it into `Options.Hooks` for the named event/matcher.
**Acceptance:**
- [ ] DI-resolved hook handler is invoked through the control protocol in an
  integration-shaped test.
**Verification:** `AddClaudeAgentHookTests`.
**Dependencies:** 12.2, 7.1.
**Files:** `src/Daystrom.ClaudeAgentSdk.DependencyInjection/ClaudeAgentServiceCollectionExtensions.cs` (partial).

### Checkpoint: Phase 12
- [ ] `samples/DependencyInjection` stub composes a `Host` and runs a query.

---

## Phase 13: Testing package

Goal: `Daystrom.ClaudeAgentSdk.Testing` ships first-class fakes for users.

### Task 13.1: `FakeTransport` — M
**Description:** `ITransport` impl backed by scripted in-memory
inbound/outbound queues. Useful for asserting "the SDK sent X" and "given Y
on the wire, the SDK yields Z".
**Acceptance:**
- [ ] Round-trip: script a sequence of CLI messages, assert SDK yields the
  matching `Message` records.
- [ ] Capture: assert the SDK sent the expected wire bytes.
**Verification:** `FakeTransportTests`.
**Dependencies:** 4.7.
**Files:** `src/Daystrom.ClaudeAgentSdk.Testing/FakeTransport.cs`.

### Task 13.2: `RecordingTransport` — S
**Description:** Decorator over `SubprocessCliTransport` that captures every
NDJSON line in/out for snapshot assertions.
**Acceptance:**
- [ ] Snapshot test of a one-shot query is byte-stable.
**Verification:** `RecordingTransportTests`.
**Dependencies:** 13.1.
**Files:** `src/Daystrom.ClaudeAgentSdk.Testing/RecordingTransport.cs`.

### Task 13.3: `ClaudeAgentClientHarness` — S
**Description:** High-level convenience wrapper that drives an
`IClaudeAgentClient` against a `FakeTransport` and exposes ergonomic
assertion helpers (`AssertSentInterrupt()`, `AssertReceivedAssistantText()`).
**Acceptance:**
- [ ] Existing internal tests for `ClaudeAgentClient` can be rewritten to
  use the harness with no loss of coverage.
**Verification:** `ClaudeAgentClientHarnessTests`.
**Dependencies:** 13.1, 6.4.
**Files:** `src/Daystrom.ClaudeAgentSdk.Testing/ClaudeAgentClientHarness.cs`.

### Checkpoint: Phase 13
- [ ] Testing package documented in README with a 10-line example.

---

## Phase 14: Samples

Goal: nine working samples from spec §6.

### Task 14.1: Sample projects (× 9) — S × 9
**Description:** One sample per directory under `samples/`. Each is a tiny
console app demonstrating exactly one feature: QuickStart, StreamingMode,
McpCalculator, Hooks, ToolPermissionCallback, Agents, Plugin, SessionResume,
DependencyInjection.
**Acceptance:**
- [ ] Each builds in CI.
- [ ] Each runs to completion under `--api-key=$DUMMY` against a stubbed
  transport (the spec §14 "sample-as-test" requirement).
**Verification:** `SampleSmokeTests` runs each sample with a dummy key and
asserts exit code 0.
**Dependencies:** All previous phases.
**Files:** 9 × `samples/<Name>/*.cs`.

### Checkpoint: Phase 14
- [ ] Samples documented in README with one-paragraph descriptions and
  `dotnet run --project samples/<Name>` invocation.

---

## Phase 15: Release pipeline + quality gates

Goal: `dotnet pack` produces signed, deterministic, source-link-enabled,
SBOM-bearing packages; tagged push triggers `dotnet nuget push`.

### Task 15.1: Determinism + Source Link + SBOM — M
**Description:** `Directory.Build.props` updates: `Deterministic=true`,
`ContinuousIntegrationBuild=true` (CI-only), `PublishRepositoryUrl=true`,
`EmbedUntrackedSources=true`. Add `Microsoft.Sbom.Targets` to all packable
projects.
**Acceptance:**
- [ ] Two CI builds of the same commit produce byte-identical `.nupkg`.
- [ ] Source Link works in a consumer project (debugger steps into SDK).
- [ ] SBOM appears in each package.
**Verification:** `nuget verify` + manual debugger session.
**Dependencies:** 1.2.
**Files:** `Directory.Build.props`.

### Task 15.2: NuGet signing — S
**Description:** Sign all four user-facing packages + five native packages
with the project's NuGet signing certificate before push. Cert handled via
GitHub secret.
**Acceptance:**
- [ ] `dotnet nuget verify` accepts every produced package.
**Verification:** Local push to a private feed; verification succeeds.
**Dependencies:** 15.1.
**Files:** `.github/workflows/release.yml`.

### Task 15.3: Release workflow — M
**Description:** `.github/workflows/release.yml` triggered by `v*` tags.
Builds, tests, packs, signs, and pushes all packages to nuget.org. Requires
`NUGET_API_KEY` secret. Includes a dry-run mode triggered via
`workflow_dispatch`.
**Acceptance:**
- [ ] Dry-run on a throwaway tag produces all packages but skips push.
- [ ] Real release of `v0.1.0-preview1` to nuget.org succeeds.
**Verification:** Tag-push smoke test on `v0.0.0-test`.
**Dependencies:** 15.1, 15.2, 2.3, 14.1.
**Files:** `.github/workflows/release.yml`.

### Task 15.4: AOT / trim warning gate in CI — S
**Description:** Add a CI job that runs `dotnet publish -c Release
-r linux-x64 --self-contained -p:PublishAot=true` on
`samples/QuickStart` and fails the build on any `IL2*`/`IL3*` warning.
**Acceptance:**
- [ ] Job fails if any reflection-based STJ call sneaks into the core path.
**Verification:** Pre-merge required check.
**Dependencies:** 3.8.
**Files:** `.github/workflows/ci.yml` (partial).

### Checkpoint: Phase 15
- [ ] Acceptance criteria 1–8 from spec §18 verified on a release candidate.

---

## Phase 16: Documentation

### Task 16.1: README quickstart — M
**Description:** Three-example quickstart matching the Python overview:
filesystem read, hooks, MCP. Plus the `dotnet add package` instructions and
a feature-parity link to spec §19.
**Acceptance:**
- [ ] Each example compiles and runs against a stub.
**Verification:** `ReadmeExampleTests` (extract, compile, smoke-run).
**Dependencies:** All previous.
**Files:** `README.md`.

### Task 16.2: `docs/architecture.md` + migration-from-Python guide — M
**Description:** Architecture deep-dive (transport → control protocol →
surface), and a side-by-side cheat sheet for Python users (e.g. `query` →
`ClaudeAgent.QueryAsync`). Cross-references the spec §19 parity matrix.
**Acceptance:**
- [ ] Every public type from spec §19 is mentioned in one of the two docs.
**Verification:** Markdown link-check + manual review.
**Dependencies:** 16.1.
**Files:** `docs/architecture.md`, `docs/migration-from-python.md`.

### Checkpoint: Phase 16 — Release readiness
- [ ] All acceptance criteria from spec §18 (1–8) met.
- [ ] Feature-parity matrix from spec §19 100% green.
- [ ] Human review and approval before tagging `v0.1.0`.

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Python SDK 0.1.72 emits wire shapes our DTOs don't match | High | Phase 3 validates against Python fixtures; CI gate on round-trip tests. Capture and check in fixtures from a real `claude` binary in Phase 5. |
| Bundled CLI bytes differ across npm tarball publishes | Medium | Pin both the version *and* the SHA-256 in `eng/Versions.props`; `download-claude-cli.ps1` verifies. |
| Trim/AOT warnings from STJ source-gen edge cases | High | Phase 15.4 gate. Catch this every CI run, not at release. |
| `control_cancel_request` v1 no-op surprises hook authors | Low | XML doc on every hook callback type explicitly notes "may be invoked but result discarded — be idempotent". Spec §13. |
| MCP SDK source-generator interaction with our STJ context | Medium | Phase 10 prototype against the actual `ModelContextProtocol` package early; fall back to reflection-based path with annotation if generator clashes. |
| Native bundle size bloat per RID | Low | Per-package size budget in `RELEASING.md`; CI fails if any package exceeds. |
| Sandbox-on-Windows silent no-op | Low | Spec §7 caveat enforced by `ILogger.LogWarning` in `CliBinaryResolver`; test asserts the warning fires once per process start. |

## Open questions for the human

1. **Discriminator strings** for every `[JsonPolymorphic]` variant
   (spec §20). We need to capture these from real CLI emission samples
   *before* Phase 3.4 lands. Plan: run the Python SDK against a real CLI in
   Phase 1.5 (informal) and check fixtures into the test project.
2. **Snapshot baseline source for `CommandBuilder`** (Phase 4.6). Should we
   snapshot against Python's `_build_command()` output captured offline, or
   maintain hand-written expected argv? Recommend the former — less drift.
3. **Reflection-based fluent MCP `AddTool(Delegate)` (Phase 11.2)** — keep
   in v1 or punt to v0.2? Spec keeps it; suggest we ship it but mark the
   AOT-unfriendly attribute clearly so users self-select.
4. **Bench targets** (spec §20). Defer to Phase 16+ unless we see surprising
   results in Phase 5 integration runs.
5. **`SubagentStop` vs `SubagentStart` event ordering on the wire** — the
   spec lists both but doesn't enumerate which CLI versions emit which.
   Capture from CLI 2.1.126 fixtures in Phase 7.

## Parallelization opportunities

- **Phase 2** (native bundle) can run start-to-finish in parallel with Phases
  3–4. Different person/session entirely.
- **Phases 7, 8, 9, 10–11** are independent vertical slices once Phase 6 has
  landed. Up to 4 sessions can work in parallel.
- **Phase 14** (samples) is parallelizable across 9 sessions, one per sample,
  once Phase 12 lands.

## Verification checklist (planning skill §verification)

- [x] Every task has acceptance criteria
- [x] Every task has a verification step
- [x] Task dependencies are identified and ordered correctly
- [x] No task touches more than ~5 files (mechanical bags noted explicitly)
- [x] Checkpoints exist between major phases (one per phase)
- [ ] **The human has reviewed and approved the plan** ← awaiting
