using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Daystrom.ClaudeAgentSdk;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Sandbox;
using Daystrom.ClaudeAgentSdk.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Transport;

public class CliBinaryResolverTests
{
    private const string FakeHome = "/home/tester";

    [Fact]
    public void Resolve_PrefersExplicitCliPathWhenItExists()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        fs.AddFile("/opt/cli/claude");
        var resolver = NewResolver(fs);

        var options = ClaudeAgentOptions.Create().WithCliPath("/opt/cli/claude").Build();

        Assert.Equal("/opt/cli/claude", resolver.Resolve(options));
    }

    [Fact]
    public void Resolve_ExplicitCliPathThatDoesNotExist_Throws()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var resolver = NewResolver(fs);

        var options = ClaudeAgentOptions.Create().WithCliPath("/nope/claude").Build();

        var ex = Assert.Throws<CliNotFoundException>(() => resolver.Resolve(options));
        Assert.Equal("/nope/claude", ex.CliPath);
    }

    [Fact]
    public void Resolve_FallsBackToBundledNativeBinary()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var bundled = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            BinaryName
        );
        fs.AddFile(bundled);
        var resolver = NewResolver(fs);

        var options = ClaudeAgentOptions.Create().Build();

        Assert.Equal(bundled, resolver.Resolve(options));
    }

    [Fact]
    public void Resolve_FallsBackToFlattenedBundledBinary()
    {
        // When the consumer csproj sets <RuntimeIdentifier> (or runs
        // `dotnet publish -r <rid>`), NuGet flattens runtime assets into
        // BaseDir alongside the managed assemblies — there's no
        // runtimes/{rid}/native/ prefix. The resolver must still pick up
        // the bundled binary in that case before falling back to PATH.
        var fs = new InMemoryFileSystem(home: FakeHome);
        var flattened = Path.Combine(AppContext.BaseDirectory, BinaryName);
        fs.AddFile(flattened);
        var resolver = NewResolver(fs);

        var options = ClaudeAgentOptions.Create().Build();

        Assert.Equal(flattened, resolver.Resolve(options));
    }

    [Fact]
    public void Resolve_LayeredBundledBeatsFlattened()
    {
        // Both layouts shouldn't normally coexist, but if they do the
        // layered path wins because that's the canonical NuGet drop site.
        var fs = new InMemoryFileSystem(home: FakeHome);
        var layered = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            BinaryName
        );
        var flattened = Path.Combine(AppContext.BaseDirectory, BinaryName);
        fs.AddFile(layered);
        fs.AddFile(flattened);
        var resolver = NewResolver(fs);

        Assert.Equal(layered, resolver.Resolve(ClaudeAgentOptions.Create().Build()));
    }

    [Fact]
    public void Resolve_UsesPathWhenBundledMissing()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var pathDir = OperatingSystem.IsWindows() ? @"C:\tools" : "/usr/bin";
        var pathBin = Path.Combine(pathDir, BinaryName);
        fs.AddFile(pathBin);
        fs.SetEnv("PATH", pathDir);
        var resolver = NewResolver(fs);

        var options = ClaudeAgentOptions.Create().Build();

        Assert.Equal(pathBin, resolver.Resolve(options));
    }

    [Fact]
    public void Resolve_FallsBackToNpmGlobal()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var npmGlobal = Path.Combine(FakeHome, ".npm-global", "bin", "claude");
        fs.AddFile(npmGlobal);
        var resolver = NewResolver(fs);

        var options = ClaudeAgentOptions.Create().Build();

        Assert.Equal(npmGlobal, resolver.Resolve(options));
    }

    [Fact]
    public void Resolve_FallsBackToUsrLocalBin()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var usrLocal = Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "usr",
            "local",
            "bin",
            "claude"
        );
        fs.AddFile(usrLocal);
        var resolver = NewResolver(fs);

        var options = ClaudeAgentOptions.Create().Build();

        Assert.Equal(usrLocal, resolver.Resolve(options));
    }

    [Fact]
    public void Resolve_FallsBackToHomeLocalBin()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var homeLocal = Path.Combine(FakeHome, ".local", "bin", "claude");
        fs.AddFile(homeLocal);
        var resolver = NewResolver(fs);

        Assert.Equal(homeLocal, resolver.Resolve(ClaudeAgentOptions.Create().Build()));
    }

    [Fact]
    public void Resolve_FallsBackToHomeNodeModulesBin()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var path = Path.Combine(FakeHome, "node_modules", ".bin", "claude");
        fs.AddFile(path);
        var resolver = NewResolver(fs);

        Assert.Equal(path, resolver.Resolve(ClaudeAgentOptions.Create().Build()));
    }

    [Fact]
    public void Resolve_FallsBackToYarnBin()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var path = Path.Combine(FakeHome, ".yarn", "bin", "claude");
        fs.AddFile(path);
        var resolver = NewResolver(fs);

        Assert.Equal(path, resolver.Resolve(ClaudeAgentOptions.Create().Build()));
    }

    [Fact]
    public void Resolve_FallsBackToClaudeLocal()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var path = Path.Combine(FakeHome, ".claude", "local", "claude");
        fs.AddFile(path);
        var resolver = NewResolver(fs);

        Assert.Equal(path, resolver.Resolve(ClaudeAgentOptions.Create().Build()));
    }

    [Fact]
    public void Resolve_NothingFound_ThrowsWithEveryAttemptedPath()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        fs.SetEnv("PATH", "/path/dir-a" + Path.PathSeparator + "/path/dir-b");
        var resolver = NewResolver(fs);

        var ex = Assert.Throws<CliNotFoundException>(() =>
            resolver.Resolve(ClaudeAgentOptions.Create().Build())
        );

        var message = ex.Message;
        Assert.Contains("Paths attempted", message);
        Assert.Contains(BinaryName, message);
        Assert.Contains(Path.Combine(AppContext.BaseDirectory, BinaryName), message);
        Assert.Contains(Path.Combine(FakeHome, ".npm-global", "bin", "claude"), message);
        Assert.Contains(Path.Combine(FakeHome, ".claude", "local", "claude"), message);
        Assert.Contains("/path/dir-a", message);
        Assert.Contains("/path/dir-b", message);
    }

    [Fact]
    public void Resolve_OrderingPrefersBundledOverPath()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var bundled = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            BinaryName
        );
        var pathDir = OperatingSystem.IsWindows() ? @"C:\tools" : "/usr/bin";
        var pathBin = Path.Combine(pathDir, BinaryName);
        fs.AddFile(bundled);
        fs.AddFile(pathBin);
        fs.SetEnv("PATH", pathDir);
        var resolver = NewResolver(fs);

        Assert.Equal(bundled, resolver.Resolve(ClaudeAgentOptions.Create().Build()));
    }

    [Fact]
    public void Resolve_OrderingPrefersPathOverFallbacks()
    {
        var fs = new InMemoryFileSystem(home: FakeHome);
        var pathDir = OperatingSystem.IsWindows() ? @"C:\tools" : "/usr/bin";
        var pathBin = Path.Combine(pathDir, BinaryName);
        fs.AddFile(pathBin);
        fs.AddFile(Path.Combine(FakeHome, ".npm-global", "bin", "claude"));
        fs.SetEnv("PATH", pathDir);
        var resolver = NewResolver(fs);

        Assert.Equal(pathBin, resolver.Resolve(ClaudeAgentOptions.Create().Build()));
    }

    [Fact]
    public void Resolve_SandboxOnWindows_LogsWarningOnce()
    {
        // The native-Windows caveat is platform-conditional in production
        // but the resolver only checks OperatingSystem.IsWindows(). On
        // non-Windows hosts the warning never fires, so we verify the
        // *non-firing* path here and rely on the dedicated XML-doc test
        // (Sandbox/SandboxXmlDocCaveatTest) to guard the documented
        // contract on every platform.
        var fs = new InMemoryFileSystem(home: FakeHome);
        var bundled = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            BinaryName
        );
        fs.AddFile(bundled);
        var logger = new RecordingLogger();
        var resolver = NewResolver(fs, logger);

        var sandbox = new SandboxSettings { Enabled = true };
        var options = ClaudeAgentOptions.Create().WithSandbox(sandbox).Build();

        resolver.Resolve(options);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(
                logger.Warnings,
                w => w.Contains("native Windows", StringComparison.Ordinal)
            );
            // Second resolve: warning already fired this process, must not fire again.
            logger.Warnings.Clear();
            resolver.Resolve(options);
            Assert.Empty(logger.Warnings);
        }
        else
        {
            Assert.DoesNotContain(
                logger.Warnings,
                w => w.Contains("native Windows", StringComparison.Ordinal)
            );
        }
    }

    [Fact]
    public void Resolve_VersionCheckIsSkippedWhenEnvVarSet()
    {
        // When the skip env var is present, the version probe must not
        // run — verified by overriding MaybeCheckVersion and counting
        // calls in the spying subclass.
        var fs = new InMemoryFileSystem(home: FakeHome);
        var bundled = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            BinaryName
        );
        fs.AddFile(bundled);
        fs.SetEnv("CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK", "1");

        var resolver = new SpyingResolver(fs);
        resolver.Resolve(ClaudeAgentOptions.Create().Build());

        // The base class's MaybeCheckVersion early-returns based on env var,
        // but our spy proves the *resolver* invoked it (so the gating logic
        // reaches the env check).
        Assert.Equal(1, resolver.VersionCheckInvocations);
        Assert.True(resolver.LastSkippedByEnv);
    }

    private static CliBinaryResolver NewResolver(IFileSystem fs, ILogger? logger = null) =>
        new TestResolver(fs, logger);

    private static string BinaryName => OperatingSystem.IsWindows() ? "claude.exe" : "claude";

    /// <summary>
    /// CliBinaryResolver subclass that suppresses the live <c>claude -v</c>
    /// process spawn during tests — the lookup logic is what we're
    /// verifying, not the version probe.
    /// </summary>
    private sealed class TestResolver : CliBinaryResolver
    {
        public TestResolver(IFileSystem fs, ILogger? logger)
            : base(fs, logger ?? NullLogger.Instance) { }

        protected internal override void MaybeCheckVersion(string cliPath)
        {
            // No-op for unit tests.
        }
    }

    private sealed class SpyingResolver : CliBinaryResolver
    {
        private readonly IFileSystem _fs;

        public SpyingResolver(IFileSystem fs)
            : base(fs)
        {
            _fs = fs;
        }

        public int VersionCheckInvocations { get; private set; }
        public bool LastSkippedByEnv { get; private set; }

        protected internal override void MaybeCheckVersion(string cliPath)
        {
            VersionCheckInvocations++;
            LastSkippedByEnv = !string.IsNullOrEmpty(
                _fs.GetEnvironmentVariable(SkipVersionCheckEnvVar)
            );
            // Don't call base — we don't want to spawn a process here.
        }
    }

    private sealed class InMemoryFileSystem : IFileSystem
    {
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _env = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _home;

        public InMemoryFileSystem(string home)
        {
            _home = home;
        }

        public void AddFile(string path) => _files.Add(path);

        public void SetEnv(string name, string value) => _env[name] = value;

        public bool FileExists(string path) => _files.Contains(path);

        public string? GetEnvironmentVariable(string name) =>
            _env.TryGetValue(name, out var value) ? value : null;

        public string GetUserHome() => _home;

        public string ReadAllText(string path) =>
            throw new System.NotImplementedException(
                "Resolver tests don't read files; use a separate fake if needed."
            );
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose() { }
        }
    }
}
