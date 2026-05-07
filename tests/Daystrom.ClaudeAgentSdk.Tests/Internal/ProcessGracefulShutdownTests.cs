using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Internal;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Internal;

public class ProcessGracefulShutdownTests
{
    private static readonly TimeSpan TestStepTimeout = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task Shutdown_ExitOnEofChild_DoesNotEscalateToKill()
    {
        using var process = StartFixture("exit-on-eof");

        var sw = Stopwatch.StartNew();
        await new ProcessGracefulShutdown(process, TestStepTimeout).ShutdownAsync();
        sw.Stop();

        Assert.True(process.HasExited);
        Assert.Equal(0, process.ExitCode);
        // Graceful path completes in well under one step-timeout because
        // the child exits immediately on stdin EOF.
        Assert.True(
            sw.Elapsed < TestStepTimeout + TestStepTimeout,
            $"Graceful shutdown took {sw.Elapsed} (expected < {TestStepTimeout * 2})"
        );
    }

    [Fact]
    public async Task Shutdown_IgnoreEofChild_EscalatesToKill()
    {
        using var process = StartFixture("ignore-eof");

        var sw = Stopwatch.StartNew();
        await new ProcessGracefulShutdown(process, TestStepTimeout).ShutdownAsync();
        sw.Stop();

        Assert.True(process.HasExited);
        // The child ignores stdin EOF, so the helper had to wait at least
        // one step-timeout before issuing Kill.
        Assert.True(
            sw.Elapsed >= TestStepTimeout,
            $"Expected at least one step-timeout to elapse before kill, got {sw.Elapsed}"
        );
    }

    [Fact]
    public async Task Shutdown_IsIdempotent()
    {
        using var process = StartFixture("ignore-eof");
        var helper = new ProcessGracefulShutdown(process, TestStepTimeout);

        await helper.ShutdownAsync();
        Assert.True(helper.HasShutdown);
        Assert.True(process.HasExited);

        // Second call must not throw or block.
        await helper.ShutdownAsync();
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task Shutdown_RespectsCancellation()
    {
        using var process = StartFixture("ignore-eof");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Cancellation forwards into WaitForExitAsync; the helper still
        // finishes the kill ladder afterwards. We assert it terminates
        // without deadlocking.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new ProcessGracefulShutdown(process, TestStepTimeout).ShutdownAsync(cts.Token)
        );

        // Background cleanup: ensure the child is reaped before the test exits.
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // already gone
        }
    }

    [Fact]
    public void Constructor_RejectsNonPositiveTimeout()
    {
        using var process = new Process();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessGracefulShutdown(process, TimeSpan.Zero)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessGracefulShutdown(process, TimeSpan.FromMilliseconds(-1))
        );
    }

    private static Process StartFixture(string mode)
    {
        var fixtureDll = SubprocessFixturePath.Resolve();
        var psi = new ProcessStartInfo("dotnet", $"\"{fixtureDll}\" {mode}")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var process = Process.Start(psi);
        Assert.NotNull(process);
        return process!;
    }

    /// <summary>
    /// Locates the SubprocessFixture binary relative to the test
    /// assembly's runtime location. Both projects share Configuration +
    /// TargetFramework, so the path is deterministic.
    /// </summary>
    private static class SubprocessFixturePath
    {
        public static string Resolve()
        {
            var testAssemblyDir = Path.GetDirectoryName(
                typeof(ProcessGracefulShutdownTests).Assembly.Location
            )!;
            // testAssemblyDir = .../tests/Daystrom.ClaudeAgentSdk.Tests/bin/{Config}/net10.0
            var tfmDir = testAssemblyDir;
            var configDir = Path.GetDirectoryName(tfmDir)!;
            var binDir = Path.GetDirectoryName(configDir)!;
            var testProjectDir = Path.GetDirectoryName(binDir)!;
            var testsRoot = Path.GetDirectoryName(testProjectDir)!;
            var fixturePath = Path.Combine(
                testsRoot,
                "Daystrom.ClaudeAgentSdk.Tests.SubprocessFixture",
                "bin",
                Path.GetFileName(configDir),
                Path.GetFileName(tfmDir),
                "Daystrom.ClaudeAgentSdk.Tests.SubprocessFixture.dll"
            );
            if (!File.Exists(fixturePath))
            {
                throw new FileNotFoundException(
                    "SubprocessFixture binary not found; was the test project's "
                        + "ProjectReference to it dropped? Expected: "
                        + fixturePath
                );
            }
            return fixturePath;
        }
    }
}
