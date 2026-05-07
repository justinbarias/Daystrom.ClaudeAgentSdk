# Wire-protocol fixtures

Captured / extracted JSON snippets used by `MessageParserTests`,
`ContentBlockTests`, and the rest of the round-trip suite.

## Provenance

These were transcribed from the Python SDK's test suite at
`external/claude-agent-sdk-py/tests/test_message_parser.py` (pinned to
v0.1.72 / CLI 2.1.126). The Python tests are themselves pinned
representations of CLI emission — what the CLI sends in real runs — so
they're as close to "captured from the wire" as we can get without
spending API credits on every test run.

When the bundled CLI version bumps, these should be re-verified. The
preferred regeneration path is `eng/capture-fixtures.ps1` against a real
`claude` binary (requires `ANTHROPIC_API_KEY`).

## Layout

- `discriminators.md` — authoritative table of `[JsonPolymorphic]`
  discriminator strings. Read this before adding `[JsonDerivedType]`
  attributes.
- `content/<variant>.json` — one fixture per `ContentBlock` variant.
- `message/<variant>.json` — one fixture per top-level `Message` variant
  (and per system subtype).
- (future) `hook/`, `mcp/`, `thinking/`, `permission/` — fixtures for
  variants that can't be elicited from `--print` mode and need a
  streaming + control-protocol capture (Phases 7 / 10).

All `*.json` files in this tree are embedded as resources by the test
csproj, so tests load them via `typeof(...).Assembly.GetManifestResourceStream`
and don't depend on filesystem layout at runtime.
