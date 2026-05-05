# Releasing

This document tracks the release process for `claude-agent-sdk-dotnet`.

## Versioning

- The SDK version lives in `eng/Versions.props` (`<SdkVersion>`).
- The bundled CLI version lives in the same file (`<ClaudeCliVersion>`).
- The two are independent; both appear in package readmes.
- Sub-packages move in lockstep with the core via Central Package Management.

## Pre-release checklist

- [ ] All tests pass on Linux, macOS, and Windows in CI.
- [ ] AOT publish of `samples/QuickStart` produces zero `IL2*` / `IL3*`
      warnings (Phase 15.4 gate).
- [ ] `CHANGELOG.md` has a section for the version being released, with a
      migration note if any breaking changes are present.
- [ ] All four user-facing packages and all five RID native sub-packages
      pack within size budgets (see [Package size budgets](#package-size-budgets)).
- [ ] Deterministic build verified: two CI builds of the same commit
      produce byte-identical `.nupkg` files.
- [ ] Source Link verified by stepping into SDK code from a sample
      consumer project's debugger.
- [ ] SBOM present in each package.
- [ ] All packages signed with the project's NuGet signing certificate.

## Release procedure

1. Update `<SdkVersion>` in `eng/Versions.props` (and `<ClaudeCliVersion>`
   if rolling forward to a new CLI).
2. Update `CHANGELOG.md` — move `[Unreleased]` items under the new version
   header with the release date.
3. Open a release PR; merge once CI is green.
4. Tag the merge commit: `git tag v<version>`; push the tag.
5. The `release.yml` workflow builds, signs, and pushes all packages to
   nuget.org.
6. After publication, verify each package on nuget.org and update the
   release notes on GitHub.

## Package size budgets

To be set when each package first ships (Phase 15). Rough placeholders:

| Package | Budget |
|---|---|
| `Anthropic.ClaudeAgentSdk` | 500 KB |
| `Anthropic.ClaudeAgentSdk.Mcp` | 100 KB |
| `Anthropic.ClaudeAgentSdk.DependencyInjection` | 50 KB |
| `Anthropic.ClaudeAgentSdk.Testing` | 100 KB |
| `runtime.{rid}.Anthropic.ClaudeAgentSdk.Native` | TBD per RID |

CI fails if any package exceeds its budget by more than 10%.

## Native bundle

See [`eng/download-claude-cli.ps1`](eng/download-claude-cli.ps1) and
[`.github/workflows/native-bundle.yml`](.github/workflows/native-bundle.yml).
The pinned CLI version is verified by SHA-256 against the upstream npm
tarball before each release.
