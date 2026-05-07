# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project at a glance

`claude-agent-sdk-dotnet` is a .NET 10 wrapper SDK for the Claude Agent SDK.
It does **not** reimplement the agent loop — it spawns the bundled Claude
Code Node CLI as a subprocess and speaks the same `stream-json` NDJSON wire
protocol as the Python SDK. The CLI is the source of truth.

Authoritative docs (read these before non-trivial work; do not duplicate
their content here):

- Design spec: [`docs/superpowers/specs/2026-05-05-claude-agent-sdk-dotnet-design.md`](docs/superpowers/specs/2026-05-05-claude-agent-sdk-dotnet-design.md)
- Implementation plan: [`docs/superpowers/plans/2026-05-05-claude-agent-sdk-dotnet-implementation-plan.md`](docs/superpowers/plans/2026-05-05-claude-agent-sdk-dotnet-implementation-plan.md)
- Release process: [`RELEASING.md`](RELEASING.md)
- Public README: [`README.md`](README.md)
- Pinned versions (SDK + bundled CLI): [`eng/Versions.props`](eng/Versions.props)

The plan delivers in 16 vertical phases. Phase 1 is done; Phase 2 (native
bundle) and Phase 3 (wire types) are the next slices. **Always check the
plan for the current phase before starting work** — feature work landing
out of phase order tends to grow scaffolding that later phases need to
delete.

---

## Common commands

Toolchain is pinned: .NET 10 SDK via `global.json`, CSharpier as a local
tool. Restore tools once per clone:

```sh
dotnet tool restore
```

Day-to-day:

```sh
dotnet build                                # Debug build, all projects
dotnet build --configuration Release        # what CI runs
dotnet test                                 # all tests
dotnet test --no-build --logger "console;verbosity=normal"
dotnet test path/to/Project.Tests.csproj    # one project
dotnet test --filter "FullyQualifiedName~CommandBuilderTests"   # one class/method
dotnet csharpier format .                   # format
dotnet csharpier check .                    # CI's formatting gate
dotnet pack --configuration Release         # produce .nupkg + .snupkg
```

Integration tests are gated behind `CLAUDE_INTEGRATION=1` and a real
`ANTHROPIC_API_KEY`; they only run in the Linux CI job.

CI (`.github/workflows/ci.yml`) runs on `ubuntu-latest`, `macos-latest`, and
`windows-latest`. **Anything you change must build and test clean on all
three** — file paths, process spawning, env handling, and the CLI resolver
all have OS-specific branches.

---

## Architectural rails (non-negotiable)

These constraints come from the spec and shape how code must be written.
Violating them creates rework, not just style nits.

**1. Python-SDK parity over .NET cleverness.** Internals in
`Anthropic.ClaudeAgentSdk` mirror `claude_agent_sdk/_internal/` closely so
upstream changes can be tracked mechanically. When in doubt, read the
Python source and match its structure. Idiomaticity belongs at the public
API surface (records, `IAsyncEnumerable<T>`, fluent builders, attributes,
`CancellationToken`, `*Async` suffix) — not in the transport, command
builder, or control protocol.

**2. The core package is AOT-compatible.** `Anthropic.ClaudeAgentSdk`
declares `<IsAotCompatible>true</IsAotCompatible>` and Phase 15.4 enforces
zero `IL2*`/`IL3*` warnings. Concretely: no reflection on hot paths, all
wire JSON goes through `ClaudeAgentJsonContext` (STJ source-gen) with
`[JsonPolymorphic]` discriminators, and any reflection-based API (e.g.
`SdkMcpServerBuilder.AddTool(Delegate)`) must be annotated
`[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` and live in a
non-AOT package (`.Mcp`).

**3. `TreatWarningsAsErrors=true` + nullable enabled, repo-wide.** Set in
`Directory.Build.props`. Don't suppress warnings to make a build green —
fix the cause. Don't disable nullable in new files.

**4. Central Package Management.** Versions live in
`Directory.Packages.props`. Add `<PackageReference>` without a `Version`
attribute, and add the `<PackageVersion>` entry to the central file.

**5. Deterministic, source-linked builds.** Don't add timestamps, machine
paths, or non-deterministic generators to the build. `Deterministic=true`
and Source Link are pre-wired in `Directory.Build.props`.

**6. The CLI resolver order is load-bearing.** `CliBinaryResolver` lookup
order is: `options.CliPath` → bundled native at
`AppContext.BaseDirectory/runtimes/{rid}/native/claude{,.exe}` → `PATH` →
the same npm/local fallback list Python uses. Don't reorder this; it's
what lets `dotnet add package Anthropic.ClaudeAgentSdk` work with no
extra installs.

**7. Sandbox is a no-op on native Windows.** The CLI doesn't sandbox on
Windows (only macOS/Linux/WSL2). Serialize the settings anyway, but log a
one-shot warning at process start when `Sandbox` is non-null and the host
is native Windows. Document this on every public sandbox type.

**8. One-shot fast path is real, not an optimization.**
`ClaudeAgent.QueryAsync(string, ...)` runs `--print` mode when
`ControlProtocolGate.NeedsControlProtocol(options)` returns `false` (no
hooks, no `CanUseTool`, no in-process MCP, no custom transport). This is
in the public contract — don't quietly route everyone through the
streaming path.

---

## Code quality rules specific to this repo

These are easy to violate and expensive to roll back.

- **Wire protocol changes go through both ends.** Updating `CommandBuilder`
  argv, NDJSON shapes, or control-protocol subtypes requires updating the
  matching parser/test fixtures in the same change. Mismatches surface
  late, in integration tests, on one OS.
- **Every public type needs an XML doc comment** (acceptance criterion in
  spec §18). New types without doc comments will block the release.
- **Snake-case wire names, PascalCase .NET names.** Handled centrally by
  `JsonNamingPolicy.SnakeCaseLower` on the source-gen context. Use
  `[JsonPropertyName]` only for keyword collisions, not for stylistic
  preference.
- **Discriminated unions use `[JsonPolymorphic]` + `[JsonDerivedType]`** —
  not `JsonConverter` subclasses. Adding a variant means adding it to the
  `JsonContext` AND the `[JsonDerivedType]` list on the base record.
- **Async lock on stdin writes.** All writes to the CLI's stdin go through
  the transport's write lock (mirrors Python's `_write_lock`). Don't add
  a second writer path.
- **Graceful shutdown sequence is fixed:** stdin EOF → `WaitForExitAsync(5s)`
  → `Process.Kill(false)` → `WaitForExitAsync(5s)` → `Process.Kill(true)`.
  Don't shorten timeouts to make tests faster — make tests use
  `FakeTransport`.
- **OTel propagation uses BCL `Activity` + `DistributedContextPropagator`.**
  Don't take a dependency on the OpenTelemetry SDK in core; that's
  reserved for a future companion package.
- **CSharpier is the only formatter.** Don't hand-tune whitespace or add
  EditorConfig rules that fight CSharpier — CI will fail
  `dotnet csharpier check .`.

---

## Testing posture

- xUnit + `Verify.Xunit` for snapshot tests (notably `CommandBuilderTests`
  asserting exact argv per option permutation).
- `FakeTransport` (in `Anthropic.ClaudeAgentSdk.Testing`) is the right tool
  for control-protocol, hook, and MCP routing tests. Don't spawn real
  processes in unit tests.
- Bug fixes get a regression test that fails before the fix and passes
  after. Wire-protocol bugs especially — they're the ones that recur.
- Sample projects in `samples/` must compile in CI; treat them as part of
  the test surface, not throwaway code.

---

## When you change the bundled CLI version

`<ClaudeCliVersion>` in `eng/Versions.props` is the single source of
truth. Bumping it requires:

1. Verifying upstream npm tarball SHA-256 (the `native-bundle.yml`
   workflow does this on release).
2. Re-running parity checks against the Python SDK's fixtures — control-
   protocol shapes and message variants change between CLI versions.
3. A note in `CHANGELOG.md` under `[Unreleased]`.

The SDK version (`<SdkVersion>`) and `<ClaudeCliVersion>` are independent —
don't bump them together by reflex.

---

## Behavioral guidelines

Adapted from the Karpathy CLAUDE.md base. These bias toward caution; for
trivial tasks, use judgment.

### Think before coding

State assumptions explicitly. If multiple interpretations exist, present
them — don't pick silently. If something is unclear, stop and name what's
confusing before guessing. Push back when an approach has clear problems;
"of course!" followed by a bad implementation helps no one.

### Simplicity first

Minimum code that solves the problem. No speculative abstractions, no
"flexibility" that wasn't requested, no error handling for impossible
scenarios. Match existing patterns in this repo (see the design spec's
project layout) rather than inventing new ones.

### Surgical changes

Touch only what the task requires. Don't "improve" adjacent code, comments,
or formatting. Don't refactor things that aren't broken. Don't delete
pre-existing dead code unless asked — mention it instead. Every changed
line should trace directly to the user's request.

### Goal-driven execution

Convert tasks into verifiable goals before starting:

- "Add validation" → write tests for invalid inputs, then make them pass.
- "Fix the bug" → write a test that reproduces it, then make it pass.
- "Refactor X" → confirm tests pass before and after.

A change is not complete until verification passes (build green, tests
green, CSharpier clean). "Looks right" is never sufficient.

### Scope discipline for this repo

Most work is part of a numbered phase in the implementation plan. Before
starting:

1. Identify which phase the task belongs to.
2. Check the plan's task list for that phase — don't reinvent tasks that
   are already specified.
3. If the work doesn't fit a phase, raise it before coding; the plan is
   the contract.
