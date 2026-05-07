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
| `Daystrom.ClaudeAgentSdk` | Core client, options, messages, transport |
| `Daystrom.ClaudeAgentSdk.Mcp` | In-process MCP server helpers |
| `Daystrom.ClaudeAgentSdk.DependencyInjection` | `services.AddClaudeAgent()` |
| `Daystrom.ClaudeAgentSdk.Testing` | `FakeTransport`, `RecordingTransport` |
| `runtime.{rid}.Daystrom.ClaudeAgentSdk.Native` | Bundled `claude` binary, per RID |

## Contributing

Code is formatted with [CSharpier](https://csharpier.com/), pinned as a
local tool in `dotnet-tools.json`. Before committing:

```sh
dotnet tool restore
dotnet csharpier format .
```

CI fails any PR that isn't CSharpier-clean (`dotnet csharpier check .`).

## License

[MIT](LICENSE)
