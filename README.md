# claude-agent-sdk-dotnet

A .NET wrapper SDK for the Claude Agent SDK. Mirrors the Python SDK's
subprocess-CLI architecture: spawns the bundled Claude Code Node CLI and
communicates with it over `stream-json` NDJSON on stdin/stdout, exposing an
idiomatic .NET API surface.

**Status:** Pre-alpha. See [`docs/superpowers/specs/`](docs/superpowers/specs/)
for the design specification and [`docs/superpowers/plans/`](docs/superpowers/plans/)
for the implementation plan.

## Target framework

`net10.0` only.

## Packages (planned)

| Package | Purpose |
|---|---|
| `Anthropic.ClaudeAgentSdk` | Core client, options, messages, transport |
| `Anthropic.ClaudeAgentSdk.Mcp` | In-process MCP server helpers |
| `Anthropic.ClaudeAgentSdk.DependencyInjection` | `services.AddClaudeAgent()` |
| `Anthropic.ClaudeAgentSdk.Testing` | `FakeTransport`, `RecordingTransport` |
| `runtime.{rid}.Anthropic.ClaudeAgentSdk.Native` | Bundled `claude` binary, per RID |

## License

[MIT](LICENSE)
