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
