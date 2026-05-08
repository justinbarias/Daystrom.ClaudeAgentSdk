#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Pack the five RID-specific runtime.{rid}.Daystrom.ClaudeAgentSdk.Native
  csprojs into NuGet packages.

.DESCRIPTION
  Implements Phase 2 / Task 2.3 (helper) of the implementation plan.

  Assumes eng/download-claude-cli.ps1 has already populated the per-RID
  payload folders. The csproj's pre-pack guard target will fail with a
  clear message if the binary is missing.

.PARAMETER Configuration
  The build configuration. Defaults to Release.

.PARAMETER OutputDir
  Directory to drop .nupkg files into. Defaults to artifacts/.

.PARAMETER Rid
  Optional list of RIDs to pack. Defaults to all five.

.PARAMETER Version
  Optional NuGet package version. When set, passed to dotnet pack as
  /p:Version=<value> so release.yml can stamp the GitHub release tag onto
  every package without editing eng/Versions.props. When unset, packages
  carry the value of <Version> from eng/Versions.props.
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputDir,
    [string[]]$Rid,
    [string]$Version
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$RuntimesRoot = Join-Path $RepoRoot 'src/runtimes'
$AllRids = @('linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64', 'win-x64')

if (-not $OutputDir) {
    $OutputDir = Join-Path $RepoRoot 'artifacts'
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

if (-not $Rid) {
    $Rid = $AllRids
}

foreach ($r in $Rid) {
    if ($AllRids -notcontains $r) {
        throw "Unknown RID '$r' (known: $($AllRids -join ', '))"
    }

    $csprojPath = Join-Path $RuntimesRoot "runtime.$r.Daystrom.ClaudeAgentSdk.Native/runtime.$r.Daystrom.ClaudeAgentSdk.Native.csproj"
    if (-not (Test-Path -LiteralPath $csprojPath)) {
        throw "Project not found: $csprojPath"
    }

    $packArgs = @(
        $csprojPath,
        '--configuration', $Configuration,
        '--output', $OutputDir,
        '/p:ContinuousIntegrationBuild=true'
    )
    if ($Version) {
        $packArgs += "/p:Version=$Version"
    }

    Write-Host "::group::pack runtime.$r.Daystrom.ClaudeAgentSdk.Native"
    & dotnet pack @packArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host '::endgroup::'
        throw "dotnet pack failed for $r (exit $LASTEXITCODE)"
    }
    Write-Host '::endgroup::'
}

Write-Host ''
Write-Host 'Produced packages:'
Get-ChildItem -LiteralPath $OutputDir -Filter '*.nupkg' |
    ForEach-Object {
        $sizeMb = [math]::Round($_.Length / 1MB, 2)
        Write-Host ("  {0,-72} {1,8} MB" -f $_.Name, $sizeMb)
    }
