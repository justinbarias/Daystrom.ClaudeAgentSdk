#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Download the pinned Claude Code CLI native binary per RID and drop it into
  the corresponding runtime.{rid}.Daystrom.ClaudeAgentSdk.Native payload
  folder.

.DESCRIPTION
  Implements Phase 2 / Task 2.1 of the implementation plan.

  Per RID:
    1. Map RID -> upstream npm package name (eng/cli-shasums.json).
    2. Fetch the tarball directly from the npm registry.
    3. Verify SHA-256 against the pinned value.
    4. Extract package/claude{,.exe} into
       src/runtimes/runtime.{rid}.Daystrom.ClaudeAgentSdk.Native/runtimes/{rid}/native/.
    5. On POSIX hosts, mark the binary executable (chmod +x). On Windows
       hosts, log a warning if a POSIX RID is requested - that case must
       be packed from a POSIX runner so NuGet preserves the executable bit.

  Idempotent: re-running with the cache populated and target binary present
  is a no-op.

.PARAMETER Version
  The CLI version to fetch. Defaults to <ClaudeCliVersion> in
  eng/Versions.props.

.PARAMETER Rid
  Optional list of RIDs to fetch. Defaults to all five supported RIDs.

.PARAMETER CacheDir
  Tarball cache directory. Defaults to eng/.cli-cache (gitignored).

.PARAMETER Force
  Re-download even when the cached tarball is present and valid.

.EXAMPLE
  pwsh ./eng/download-claude-cli.ps1

.EXAMPLE
  pwsh ./eng/download-claude-cli.ps1 -Rid linux-x64,osx-arm64 -Force
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string[]]$Rid,
    [string]$CacheDir,
    [switch]$Force
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ShasumsPath = Join-Path $RepoRoot 'eng/cli-shasums.json'
$VersionsPropsPath = Join-Path $RepoRoot 'eng/Versions.props'
$RuntimesRoot = Join-Path $RepoRoot 'src/runtimes'

if (-not $CacheDir) {
    $CacheDir = Join-Path $RepoRoot 'eng/.cli-cache'
}

function Get-PinnedVersion {
    [xml]$xml = Get-Content -LiteralPath $VersionsPropsPath -Raw
    $value = $xml.Project.PropertyGroup.ClaudeCliVersion
    if (-not $value) {
        throw "Could not read <ClaudeCliVersion> from $VersionsPropsPath"
    }
    return [string]$value.Trim()
}

function Get-Shasums {
    $raw = Get-Content -LiteralPath $ShasumsPath -Raw
    return ($raw | ConvertFrom-Json -AsHashtable)
}

function Get-PackageShortName([string]$packageId) {
    # "@anthropic-ai/claude-code-linux-x64" -> "claude-code-linux-x64"
    return ($packageId -split '/', 2)[1]
}

function Test-IsPosixRid([string]$rid) {
    return ($rid.StartsWith('linux-') -or $rid.StartsWith('osx-'))
}

function Get-FileSha256([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-DownloadAndVerify {
    param(
        [string]$Url,
        [string]$Destination,
        [string]$ExpectedSha256
    )

    if (-not $Force -and (Test-Path -LiteralPath $Destination)) {
        $cachedSha = Get-FileSha256 $Destination
        if ($cachedSha -eq $ExpectedSha256.ToLowerInvariant()) {
            Write-Host "  cached  ($([System.IO.Path]::GetFileName($Destination)))"
            return
        }
        Write-Host "  cache hash mismatch, re-downloading"
        Remove-Item -LiteralPath $Destination -Force
    }

    Write-Host "  downloading $Url"
    $tmp = "$Destination.partial"
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }

    # -UseBasicParsing for older PS compat; PS 7+ ignores it.
    Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing

    $actualSha = Get-FileSha256 $tmp
    if ($actualSha -ne $ExpectedSha256.ToLowerInvariant()) {
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        throw @"
SHA-256 mismatch for $Url
  expected: $ExpectedSha256
  actual:   $actualSha

If this is intentional (e.g. CLI version bump), update eng/cli-shasums.json
and re-run.
"@
    }

    Move-Item -LiteralPath $tmp -Destination $Destination -Force
}

function Expand-NpmTarball {
    param(
        [string]$TarballPath,
        [string]$Destination,
        [string]$BinaryName
    )

    # Layout inside the tarball: package/<binary>, package/package.json, ...
    # We extract only the binary, into Destination directly.
    $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ("claude-cli-extract-" + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $tmpDir | Out-Null
    try {
        # tar is available on every supported runner: macOS bsdtar, Linux GNU tar,
        # Windows 10+ ships bsdtar at C:\Windows\System32\tar.exe.
        & tar -xzf $TarballPath -C $tmpDir
        if ($LASTEXITCODE -ne 0) {
            throw "tar -xzf failed for $TarballPath (exit $LASTEXITCODE)"
        }

        $source = Join-Path $tmpDir "package/$BinaryName"
        if (-not (Test-Path -LiteralPath $source)) {
            throw "Expected $source after extracting $TarballPath"
        }

        New-Item -ItemType Directory -Path (Split-Path $Destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $Destination -Force
    }
    finally {
        Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Set-PosixExecutable([string]$path) {
    if ($IsWindows) {
        return $false  # Windows host cannot persist the bit; caller decides.
    }
    & chmod '+x' $path
    if ($LASTEXITCODE -ne 0) {
        throw "chmod +x failed for $path (exit $LASTEXITCODE)"
    }
    return $true
}

# --- main ---

if (-not $Version) {
    $Version = Get-PinnedVersion
}

$shasums = Get-Shasums
if (-not $shasums.ContainsKey($Version)) {
    throw "No SHA-256 entries for CLI version $Version in $ShasumsPath"
}
$entries = $shasums[$Version]

if (-not $Rid) {
    $Rid = @($entries.Keys)
}

New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null

$posixOnWindowsWarned = $false
$exitCode = 0

foreach ($r in $Rid) {
    if (-not $entries.ContainsKey($r)) {
        Write-Error "Unknown RID '$r' for CLI $Version (known: $($entries.Keys -join ', '))"
        $exitCode = 1
        continue
    }
    $entry = $entries[$r]
    $pkg = $entry.package
    $sha = $entry.sha256
    $binary = $entry.binary
    $shortName = Get-PackageShortName $pkg

    Write-Host "[$r] $pkg@$Version"

    $tarballName = "$shortName-$Version.tgz"
    $tarballPath = Join-Path $CacheDir $tarballName
    $url = "https://registry.npmjs.org/$pkg/-/$tarballName"

    Invoke-DownloadAndVerify -Url $url -Destination $tarballPath -ExpectedSha256 $sha

    $payloadDir = Join-Path $RuntimesRoot "runtime.$r.Daystrom.ClaudeAgentSdk.Native/runtimes/$r/native"
    $binaryDestination = Join-Path $payloadDir $binary

    Expand-NpmTarball -TarballPath $tarballPath -Destination $binaryDestination -BinaryName $binary

    if (Test-IsPosixRid $r) {
        $marked = Set-PosixExecutable $binaryDestination
        if (-not $marked -and -not $posixOnWindowsWarned) {
            Write-Warning @"
Running on Windows: extracted POSIX binaries are NOT marked executable.
The resulting .nupkg will not have the +x bit preserved. Pack POSIX RIDs
from a Linux runner (use .github/workflows/native-bundle.yml) before
shipping.
"@
            $posixOnWindowsWarned = $true
        }
    }

    Write-Host "  ok      $binaryDestination"
}

exit $exitCode
