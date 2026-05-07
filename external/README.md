# external/

Vendored sources used as offline references during SDK development.

## `claude-agent-sdk-py/`

Pinned to **v0.1.72** (commit `b512f25`) — the Python SDK release that paired
with bundled Claude Code CLI **2.1.126**, matching our `<ClaudeCliVersion>`
in `eng/Versions.props`.

Why this pin and not HEAD: the upstream SDK has since moved to CLI 2.1.132
(via 2.1.128 → 2.1.129 → 2.1.131). Wire shapes can change between CLI minor
versions — pinning the Python SDK to the same CLI we ship guarantees the
discriminator strings and field names transcribed in
`tests/Anthropic.ClaudeAgentSdk.Tests/Fixtures/discriminators.md` actually
match what our bundled CLI emits.

When `<ClaudeCliVersion>` is bumped:

1. `cd external/claude-agent-sdk-py && git fetch && git checkout <new-tag>`
   where `<new-tag>` is the Python SDK release that paired with the new CLI
   version.
2. Re-run the discriminator audit (compare `types.py` literals against
   `discriminators.md`) and update both the table and any drifted
   `[JsonDerivedType]` attributes.
3. Re-run `eng/capture-fixtures.ps1` (when an `ANTHROPIC_API_KEY` is
   available) to refresh the captured NDJSON.
4. `git add external/claude-agent-sdk-py && git commit`.
