using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk;
using Daystrom.ClaudeAgentSdk.Transport;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Transport;

/// <summary>
/// Phase 6 / Task 6.3 — streaming-mode hardening for
/// <see cref="SubprocessCliTransport"/>. Phase 4 wired the streaming path
/// but only one-shot mode was exercised end-to-end; before
/// <c>ControlProtocol</c> (Task 6.2) and <c>ClaudeAgentClient</c>
/// (Task 6.4) start hammering the streaming write path, these tests prove
/// the transport survives high-volume concurrent reads and writes without
/// deadlock or interleaved bytes.
/// </summary>
public class SubprocessCliTransportStreamingTests
{
    /// <summary>
    /// Test A — high-volume echo round-trip. 1000 NDJSON lines pushed
    /// through the echo fixture while a background reader drains stdout.
    /// Asserts every input is observed and no deadlock occurs.
    /// </summary>
    [Fact]
    public async Task HighVolumeEchoRoundTrip_AllLinesObserved()
    {
        const int LineCount = 1000;
        await using var transport = NewTransport(BuildFixtureArgv("echo-ndjson"));
        await transport.ConnectAsync();

        var collected = new ConcurrentBag<int>();
        using var readerCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var readerTask = Task.Run(async () =>
        {
            await foreach (var msg in transport.ReadMessagesAsync(readerCts.Token))
            {
                if (msg.TryGetProperty("i", out var idx) && idx.ValueKind == JsonValueKind.Number)
                {
                    collected.Add(idx.GetInt32());
                }
                if (collected.Count >= LineCount)
                {
                    break;
                }
            }
        });

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < LineCount; i++)
        {
            await transport.WriteAsync($"{{\"i\":{i}}}\n");
        }
        await transport.EndInputAsync();

        await readerTask.WaitAsync(TimeSpan.FromSeconds(60));
        sw.Stop();

        Assert.Equal(LineCount, collected.Count);
        var sorted = collected.OrderBy(x => x).ToArray();
        for (var i = 0; i < LineCount; i++)
        {
            Assert.Equal(i, sorted[i]);
        }
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(60),
            $"Round-trip took {sw.Elapsed} — suspected deadlock or stalled pipe."
        );
    }

    /// <summary>
    /// Test B — concurrent writes from N producers prove the
    /// <c>_writeLock</c> semaphore serialises stdin writes. 16 producers
    /// each write 100 lines, all carrying a distinct <c>producerId</c>;
    /// the echo fixture round-trips them; the reader asserts every line
    /// is parseable JSON and the per-producer counts match.
    /// </summary>
    [Fact]
    public async Task ConcurrentWritesFromManyProducers_AllLinesParseable()
    {
        const int Producers = 16;
        const int LinesPerProducer = 100;
        const int Total = Producers * LinesPerProducer;

        await using var transport = NewTransport(BuildFixtureArgv("echo-ndjson"));
        await transport.ConnectAsync();

        var perProducerCounts = new int[Producers];
        using var readerCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var seen = 0;
        var readerTask = Task.Run(async () =>
        {
            await foreach (var msg in transport.ReadMessagesAsync(readerCts.Token))
            {
                // Every echoed line MUST be parseable JSON containing a
                // numeric producerId in [0, Producers). Anything else
                // signals interleaved bytes on stdin.
                Assert.Equal(JsonValueKind.Object, msg.ValueKind);
                Assert.True(msg.TryGetProperty("producerId", out var pidEl));
                var pid = pidEl.GetInt32();
                Assert.InRange(pid, 0, Producers - 1);
                Interlocked.Increment(ref perProducerCounts[pid]);

                if (Interlocked.Increment(ref seen) >= Total)
                {
                    break;
                }
            }
        });

        var producerTasks = new Task[Producers];
        for (var p = 0; p < Producers; p++)
        {
            var pid = p;
            producerTasks[p] = Task.Run(async () =>
            {
                for (var i = 0; i < LinesPerProducer; i++)
                {
                    await transport.WriteAsync($"{{\"producerId\":{pid},\"seq\":{i}}}\n");
                }
            });
        }

        await Task.WhenAll(producerTasks).WaitAsync(TimeSpan.FromSeconds(60));
        await transport.EndInputAsync();

        await readerTask.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(Total, seen);
        for (var p = 0; p < Producers; p++)
        {
            Assert.Equal(LinesPerProducer, perProducerCounts[p]);
        }
    }

    /// <summary>
    /// Test C — graceful shutdown after a streaming exchange. Runs a small
    /// 50-line round-trip, signals stdin EOF, then disposes. Asserts the
    /// process exits within the spec §9 ladder (10s worst case; threshold
    /// 15s mirrors <c>QueryOneShotIntegrationTests</c>).
    /// </summary>
    [Fact]
    public async Task GracefulShutdown_AfterStreamingExchange_ExitsWithinLadder()
    {
        const int LineCount = 50;
        var transport = NewTransport(BuildFixtureArgv("echo-ndjson"));
        await transport.ConnectAsync();

        var collected = 0;
        using var readerCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var readerTask = Task.Run(async () =>
        {
            await foreach (var msg in transport.ReadMessagesAsync(readerCts.Token))
            {
                if (Interlocked.Increment(ref collected) >= LineCount)
                {
                    break;
                }
            }
        });

        for (var i = 0; i < LineCount; i++)
        {
            await transport.WriteAsync($"{{\"i\":{i}}}\n");
        }
        await transport.EndInputAsync();

        await readerTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(LineCount, collected);

        var sw = Stopwatch.StartNew();
        await transport.DisposeAsync();
        sw.Stop();

        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(15),
            $"DisposeAsync took {sw.Elapsed} — shutdown ladder exceeded 15s threshold."
        );
    }

    /// <summary>
    /// Test D — cancellation mid-exchange. A long-running writer pumps
    /// lines slowly until the token trips; the test asserts the writer
    /// exits cleanly (via OperationCanceledException or CliConnectionException
    /// once the lock is observed cancelled), the transport disposes within
    /// the shutdown-ladder budget, and no exception escapes the test
    /// boundary.
    /// </summary>
    [Fact]
    public async Task CancellationMidExchange_ShutsDownCleanly()
    {
        var transport = NewTransport(BuildFixtureArgv("echo-ndjson"));
        await transport.ConnectAsync();

        using var cts = new CancellationTokenSource();
        var writerTask = Task.Run(async () =>
        {
            try
            {
                for (var i = 0; ; i++)
                {
                    await transport.WriteAsync($"{{\"i\":{i}}}\n", cts.Token);
                    await Task.Delay(5, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected exit path.
            }
            catch (Daystrom.ClaudeAgentSdk.Errors.CliConnectionException)
            {
                // Also acceptable — the transport may be torn down before
                // the writer observes the cancellation token.
            }
        });

        // Let some traffic flow before cancelling so we're cancelling
        // mid-stream rather than pre-stream.
        await Task.Delay(100);
        cts.Cancel();

        await writerTask.WaitAsync(TimeSpan.FromSeconds(15));

        var sw = Stopwatch.StartNew();
        await transport.DisposeAsync();
        sw.Stop();
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(15),
            $"DisposeAsync took {sw.Elapsed} after mid-stream cancellation."
        );
    }

    private static SubprocessCliTransport NewTransport(IReadOnlyList<string> argv)
    {
        return new SubprocessCliTransport(new ClaudeAgentOptions(), argvOverride: argv);
    }

    private static IReadOnlyList<string> BuildFixtureArgv(params string[] modeAndArgs)
    {
        var fixtureDll = SubprocessFixturePath.Resolve();
        var argv = new List<string>(modeAndArgs.Length + 2) { "dotnet", fixtureDll };
        argv.AddRange(modeAndArgs);
        return argv;
    }

    /// <summary>
    /// Locator copied from <c>SubprocessCliTransportTests</c>; kept private
    /// per repo convention to avoid sharing a partial type just for a path
    /// helper.
    /// </summary>
    private static class SubprocessFixturePath
    {
        public static string Resolve()
        {
            var testAssemblyDir = Path.GetDirectoryName(
                typeof(SubprocessCliTransportStreamingTests).Assembly.Location
            )!;
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
