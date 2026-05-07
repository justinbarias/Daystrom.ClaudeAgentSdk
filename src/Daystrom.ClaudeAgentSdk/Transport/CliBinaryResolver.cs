using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Daystrom.ClaudeAgentSdk.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daystrom.ClaudeAgentSdk.Transport;

/// <summary>
/// Resolves the path to the Claude Code CLI binary the SDK should spawn.
/// Implements the spec §10 lookup order: <c>options.CliPath</c> → bundled
/// native binary at <c>AppContext.BaseDirectory/runtimes/{rid}/native/</c>
/// → <c>PATH</c> → six npm/local fallback paths.
/// </summary>
/// <remarks>
/// <para>
/// The lookup order is load-bearing — reordering it changes whether
/// <c>dotnet add package Daystrom.ClaudeAgentSdk</c> works without an
/// extra install step. Don't move steps around without updating the spec.
/// </para>
/// <para>
/// On native Windows, the CLI ignores sandbox settings (sandboxing is only
/// implemented on macOS, Linux, and WSL2). When the resolver runs on
/// native Windows with a non-null <see cref="ClaudeAgentOptions.Sandbox"/>,
/// it logs a single warning per process so users aren't silently surprised.
/// </para>
/// <para>
/// After a candidate path is selected the resolver optionally runs
/// <c>claude -v</c> and warns if the version is below the minimum the SDK
/// is tested against. Set the <c>CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK</c>
/// environment variable to any value to suppress the probe (useful in
/// constrained environments where spawning the CLI just to read its
/// version is undesirable, or in unit tests).
/// </para>
/// </remarks>
public class CliBinaryResolver
{
    /// <summary>
    /// Minimum Claude Code CLI version this SDK is tested against. Older
    /// versions still resolve but emit a warning. Mirrors
    /// <c>MINIMUM_CLAUDE_CODE_VERSION</c> in the Python SDK.
    /// </summary>
    public const string MinimumCliVersion = "2.0.0";

    /// <summary>
    /// Environment-variable name that, when set to any value, skips the
    /// post-resolution <c>claude -v</c> probe.
    /// </summary>
    public const string SkipVersionCheckEnvVar = "CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK";

    private static int s_sandboxOnWindowsWarned;

    private readonly IFileSystem _fs;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a resolver that uses the supplied filesystem abstraction
    /// (<see cref="DefaultFileSystem.Instance"/> when null) and emits
    /// log messages through <paramref name="logger"/>
    /// (<see cref="NullLogger.Instance"/> when null).
    /// </summary>
    public CliBinaryResolver(IFileSystem? fileSystem = null, ILogger? logger = null)
    {
        _fs = fileSystem ?? DefaultFileSystem.Instance;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Returns the absolute path of the CLI binary to spawn, applying the
    /// spec §10 lookup order. Throws <see cref="CliNotFoundException"/>
    /// when nothing resolves; the exception message lists every path
    /// attempted.
    /// </summary>
    public virtual string Resolve(ClaudeAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        MaybeWarnSandboxOnNativeWindows(options);

        var attempted = new List<string>();
        var binaryName = GetBinaryName();

        // 1. options.CliPath wins outright when set.
        if (!string.IsNullOrEmpty(options.CliPath))
        {
            attempted.Add(options.CliPath);
            if (_fs.FileExists(options.CliPath))
            {
                MaybeCheckVersion(options.CliPath);
                return options.CliPath;
            }
            // Explicit path that doesn't exist is a hard error — mirrors
            // Python's behaviour and prevents silent fallthrough to PATH.
            throw new CliNotFoundException(
                $"Claude Code not found at the configured CliPath",
                options.CliPath
            );
        }

        // 2. Bundled native binary, dropped by the runtime.<rid> package.
        var bundled = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            binaryName
        );
        attempted.Add(bundled);
        if (_fs.FileExists(bundled))
        {
            MaybeCheckVersion(bundled);
            return bundled;
        }

        // 3. PATH lookup. PATHEXT-style probing isn't applied because the
        //    Python SDK doesn't either; on Windows we still try claude.exe
        //    via GetBinaryName().
        var path = _fs.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            var separator = Path.PathSeparator;
            foreach (var dir in path.Split(separator))
            {
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }
                var candidate = Path.Combine(dir, binaryName);
                attempted.Add(candidate);
                if (_fs.FileExists(candidate))
                {
                    MaybeCheckVersion(candidate);
                    return candidate;
                }
            }
        }

        // 4. The six fallback paths the Python SDK probes. These all use
        //    the literal name "claude" — the npm/yarn shims on Windows
        //    don't follow this layout, so on Windows the bundled path or
        //    PATH almost always covers the user.
        var home = _fs.GetUserHome();
        foreach (var fallback in GetFallbackPaths(home))
        {
            attempted.Add(fallback);
            if (_fs.FileExists(fallback))
            {
                MaybeCheckVersion(fallback);
                return fallback;
            }
        }

        throw new CliNotFoundException(
            "Claude Code CLI not found. Install it with `npm install -g "
                + "@anthropic-ai/claude-code` or set ClaudeAgentOptions.CliPath. "
                + "Paths attempted: "
                + string.Join(", ", attempted)
        );
    }

    /// <summary>
    /// Runs <c>{cliPath} -v</c> and logs a warning when the parsed version
    /// is below <see cref="MinimumCliVersion"/>. Skipped entirely when
    /// <see cref="SkipVersionCheckEnvVar"/> is set. Marked <see langword="virtual"/>
    /// so unit tests can override the probe without spawning a process.
    /// </summary>
    protected internal virtual void MaybeCheckVersion(string cliPath)
    {
        if (!string.IsNullOrEmpty(_fs.GetEnvironmentVariable(SkipVersionCheckEnvVar)))
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(cliPath, "-v")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return;
            }
            // Keep the timeout tight: a 200ms probe is plenty for `-v`,
            // and we'd rather skip the check than hang resolution if the
            // binary is broken.
            if (!process.WaitForExit(2000))
            {
                process.Kill(entireProcessTree: true);
                return;
            }
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (TryParseSemver(output, out var major, out var minor, out var patch))
            {
                var (minMajor, minMinor, minPatch) = ParseSemver(MinimumCliVersion);
                if ((major, minor, patch).CompareTo((minMajor, minMinor, minPatch)) < 0)
                {
                    _logger.LogWarning(
                        "Claude Code CLI at {CliPath} reports version {Version}, "
                            + "which is older than the minimum {Minimum} this SDK is "
                            + "tested against. Upgrade with `npm install -g "
                            + "@anthropic-ai/claude-code`.",
                        cliPath,
                        $"{major}.{minor}.{patch}",
                        MinimumCliVersion
                    );
                }
            }
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // Version probing is best-effort; don't fail resolution if it
            // throws. Log at debug so a curious operator can opt in.
            _logger.LogDebug(
                ex,
                "Claude Code CLI version probe failed for {CliPath}; continuing.",
                cliPath
            );
        }
    }

    private void MaybeWarnSandboxOnNativeWindows(ClaudeAgentOptions options)
    {
        if (options.Sandbox is null)
        {
            return;
        }
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        // One warning per process — repeated session creation shouldn't
        // spam the log.
        if (System.Threading.Interlocked.Exchange(ref s_sandboxOnWindowsWarned, 1) == 0)
        {
            _logger.LogWarning(
                "ClaudeAgentOptions.Sandbox is set, but the Claude Code CLI does not "
                    + "implement sandboxing on native Windows. The settings will be "
                    + "serialised and forwarded but the CLI will ignore them. Use "
                    + "macOS, Linux, or WSL2 to enable sandboxing."
            );
        }
    }

    private static string GetBinaryName() => OperatingSystem.IsWindows() ? "claude.exe" : "claude";

    private static IEnumerable<string> GetFallbackPaths(string home)
    {
        // Order mirrors `_find_cli` in subprocess_cli.py.
        yield return Path.Combine(home, ".npm-global", "bin", "claude");
        yield return Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "usr",
            "local",
            "bin",
            "claude"
        );
        yield return Path.Combine(home, ".local", "bin", "claude");
        yield return Path.Combine(home, "node_modules", ".bin", "claude");
        yield return Path.Combine(home, ".yarn", "bin", "claude");
        yield return Path.Combine(home, ".claude", "local", "claude");
    }

    private static bool TryParseSemver(string raw, out int major, out int minor, out int patch)
    {
        major = minor = patch = 0;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }
        // The CLI's `-v` output is typically "claude 2.1.126" or just "2.1.126".
        // Pull out the first dotted-numeric run.
        var span = raw.AsSpan();
        var start = 0;
        while (start < span.Length && (span[start] < '0' || span[start] > '9'))
        {
            start++;
        }
        if (start == span.Length)
        {
            return false;
        }
        var end = start;
        while (end < span.Length && (span[end] is '.' or (>= '0' and <= '9')))
        {
            end++;
        }
        var version = span.Slice(start, end - start);
        var parts = version.ToString().Split('.');
        if (parts.Length < 1)
        {
            return false;
        }
        return int.TryParse(parts[0], out major)
            && (parts.Length < 2 || int.TryParse(parts[1], out minor) || (minor = 0) == 0)
            && (parts.Length < 3 || int.TryParse(parts[2], out patch) || (patch = 0) == 0);
    }

    private static (int Major, int Minor, int Patch) ParseSemver(string raw)
    {
        var parts = raw.Split('.');
        return (
            parts.Length > 0 && int.TryParse(parts[0], out var ma) ? ma : 0,
            parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0,
            parts.Length > 2 && int.TryParse(parts[2], out var pa) ? pa : 0
        );
    }
}
