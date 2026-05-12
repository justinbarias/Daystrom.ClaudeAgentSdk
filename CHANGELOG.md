# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial design specification (`docs/superpowers/specs/`).
- Implementation plan (`docs/superpowers/plans/`).
- Repository foundation: `global.json`, `LICENSE`, `README.md`,
  `CHANGELOG.md`, `RELEASING.md`, `.gitignore`, `nuget.config`,
  empty solution.
- Phase 2: native bundle pipeline. Five RID-specific NuGet packages
  (`runtime.{linux-x64,linux-arm64,osx-x64,osx-arm64,win-x64}.Daystrom.ClaudeAgentSdk.Native`)
  pack the pinned `claude` CLI binary (currently `2.1.126`) for consumer-
  side runtime resolution. Backed by `eng/download-claude-cli.ps1`
  (cross-platform, SHA-256 verified against `eng/cli-shasums.json`),
  `eng/pack-native.ps1`, and `.github/workflows/native-bundle.yml`
  (tag- and dispatch-triggered).
- Phase 6: streaming client + control-protocol baseline.
  `IClaudeAgentClient` interface and `ClaudeAgentClient` implementation
  for multi-turn streaming sessions, with `ConnectAsync`,
  `SendUserMessageAsync`, `ReceiveMessagesAsync`, `EndInputAsync`,
  `DisconnectAsync`, `InterruptAsync`, `GetMcpStatusAsync`,
  `GetContextUsageAsync`, and `ApplyPermissionUpdateAsync` (mode updates
  only — rule and additional-directory updates deferred to Phase 8).
- `UserMessageInput` record for typed streaming-mode user messages.
- `ClaudeAgent.QueryAsync(IAsyncEnumerable<UserMessageInput>, …)`
  overload that always uses the streaming control-protocol path.
- `samples/StreamingMode`: AOT-clean demo that drives
  `ClaudeAgentClient` end-to-end against an in-process scripted
  `ITransport` — runs without a Node CLI or credentials and exercises
  the public `Create(options, transport)` factory.

### Changed

- `ClaudeAgent.QueryAsync(string, …)` no longer throws
  `NotSupportedException` when the options enable hooks, `CanUseTool`,
  or in-process MCP servers. Such calls now transparently route through
  the streaming control protocol; the gate-false fast path
  (`--print` mode) is unchanged.
- `ClaudeAgent.QueryAsync(string, …)` is now `async
  IAsyncEnumerable<Message>`. `ArgumentNullException` for a null prompt
  is observed on the first `MoveNextAsync` rather than at the call site,
  matching every other async iterator in the SDK.

### Internal

- Control-protocol layer (`Daystrom.ClaudeAgentSdk.Control`):
  `ControlMessages.cs` plus eight derived `ControlRequestPayload` types,
  and `ControlProtocol.cs` for request/response correlation, in-flight
  cancel propagation matching Python's `_internal/query.py:272-277`,
  write-lock serialized stdin, and a channel-backed read loop for SDK
  messages.
