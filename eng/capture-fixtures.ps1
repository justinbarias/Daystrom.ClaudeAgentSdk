<#
.SYNOPSIS
    Captures wire-protocol fixtures from the bundled `claude` CLI.

.DESCRIPTION
    Runs the bundled CLI under `--print --output-format stream-json` against
    a small set of scripted prompts chosen to elicit each top-level Message
    variant and ContentBlock variant the SDK needs to round-trip through
    its source-gen serializer. Captured NDJSON is split per-variant and
    written under tests/Anthropic.ClaudeAgentSdk.Tests/Fixtures/.

    Prerequisite: the ANTHROPIC_API_KEY environment variable must be set,
    and a bundled `claude` binary must exist at:
        src/runtimes/runtime.<host-rid>/runtimes/<host-rid>/native/claude{,.exe}

    Run frequency: once per `<ClaudeCliVersion>` bump in eng/Versions.props.
    Captured fixtures are checked into the repo so CI does not need an API
    key to run the round-trip tests.

.PARAMETER OutputDir
    Override the default output directory.

.PARAMETER ClaudePath
    Override the resolved CLI path.

.NOTES
    This script is the SDK's `fixture regeneration` story; if you find a
    real wire shape that fixtures don't cover, add a prompt below and
    re-run. Don't hand-edit the captured JSON — round-trip semantics
    depend on representing exactly what the CLI sends.
#>

[CmdletBinding()]
param(
    [string]$OutputDir = "$PSScriptRoot/../tests/Anthropic.ClaudeAgentSdk.Tests/Fixtures",
    [string]$ClaudePath
)

$ErrorActionPreference = 'Stop'

if (-not $env:ANTHROPIC_API_KEY) {
    throw "ANTHROPIC_API_KEY is not set. Capture requires a real key — fixtures get checked in once."
}

# Resolve the bundled CLI for the current host RID.
if (-not $ClaudePath) {
    $rid = & dotnet --info |
        Select-String -Pattern '^\s+RID:\s+(\S+)' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Select-Object -First 1
    if (-not $rid) { throw "Unable to determine host RID from `dotnet --info`." }

    $exe = if ($IsWindows) { 'claude.exe' } else { 'claude' }
    $ClaudePath = "$PSScriptRoot/../src/runtimes/runtime.$rid.Anthropic.ClaudeAgentSdk.Native/runtimes/$rid/native/$exe"
}

if (-not (Test-Path $ClaudePath)) {
    throw "Bundled CLI not found at $ClaudePath. Run eng/download-claude-cli.ps1 first."
}

Write-Host "Using CLI: $ClaudePath"
Write-Host "Output:    $OutputDir"

$prompts = @(
    @{
        Name = 'plain_text'
        Description = 'Elicits a basic AssistantMessage with TextBlock + ResultMessage.'
        Prompt = 'Reply with exactly the word: hello'
    },
    @{
        Name = 'tool_use_bash'
        Description = 'Elicits ToolUseBlock + ToolResultBlock for a Bash call.'
        Prompt = 'Run `echo capture-fixture` using the Bash tool and report the output.'
    },
    @{
        Name = 'thinking'
        Description = 'Elicits ThinkingBlock when extended thinking is enabled.'
        Prompt = 'Think carefully then say: ok.'
    }
)

New-Item -ItemType Directory -Force -Path "$OutputDir/raw" | Out-Null

foreach ($p in $prompts) {
    $rawPath = Join-Path "$OutputDir/raw" "$($p.Name).ndjson"
    Write-Host "`n>>> Capturing '$($p.Name)' — $($p.Description)"

    & $ClaudePath `
        --print `
        --output-format stream-json `
        --input-format text `
        $p.Prompt `
        | Tee-Object -FilePath $rawPath | Out-Null

    Write-Host "    Wrote $rawPath"
}

Write-Host "`nCapture complete. Inspect raw/*.ndjson and split into per-variant files under message/, content/, etc."
Write-Host "DO NOT auto-overwrite curated fixture files — review and split manually so each fixture is one variant."
