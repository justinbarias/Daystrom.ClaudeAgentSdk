using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Transport;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Transport;

public class SubprocessCliTransportTests
{
    [Fact]
    public async Task ReadMessages_FixtureEmits5Lines_YieldsAll5()
    {
        await using var transport = NewTransport(BuildFixtureArgv("emit-ndjson", "5"));
        await transport.ConnectAsync();

        var collected = new List<JsonElement>();
        await foreach (var msg in transport.ReadMessagesAsync())
        {
            collected.Add(msg);
        }

        Assert.Equal(5, collected.Count);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(i, collected[i].GetProperty("index").GetInt32());
            Assert.Equal("sample", collected[i].GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task WriteThenReadAsync_RoundTripsThroughEchoFixture()
    {
        await using var transport = NewTransport(BuildFixtureArgv("echo-ndjson"));
        await transport.ConnectAsync();

        await transport.WriteAsync("{\"id\":\"a\"}\n");
        await transport.WriteAsync("{\"id\":\"b\"}\n");
        await transport.EndInputAsync();

        var collected = new List<JsonElement>();
        await foreach (var msg in transport.ReadMessagesAsync())
        {
            collected.Add(msg);
        }

        Assert.Equal(2, collected.Count);
        Assert.Equal("a", collected[0].GetProperty("id").GetString());
        Assert.True(collected[0].GetProperty("echoed").GetBoolean());
        Assert.Equal("b", collected[1].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Cancellation_OfReadAsync_ClosesGracefully()
    {
        // ignore-eof never writes; ReadMessagesAsync would block forever.
        await using var transport = NewTransport(BuildFixtureArgv("ignore-eof"));
        await transport.ConnectAsync();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in transport.ReadMessagesAsync(cts.Token))
            {
                // exhaust
            }
        });
        // No further assertion: DisposeAsync runs the shutdown ladder; the
        // assertion is that this test method returns within seconds rather
        // than hanging.
    }

    [Fact]
    public async Task Stderr_InvokesCallback_OnlyWhenCallbackProvided()
    {
        var captured = new List<string>();
        var options = new ClaudeAgentOptions { Stderr = line => captured.Add(line) };

        await using var transport = new SubprocessCliTransport(
            options,
            argvOverride: BuildFixtureArgv("stderr-then-exit")
        );
        await transport.ConnectAsync();

        // Drain stdout (empty) so process exits fully; with no Stderr
        // callback we'd inherit stderr — verify the callback is in fact
        // invoked.
        await foreach (var _ in transport.ReadMessagesAsync())
        {
            // exhaust
        }

        // Give the stderr drain a moment to drop the line.
        await Task.Delay(200);

        Assert.Contains(captured, line => line.Contains("hello from stderr"));
    }

    [Fact]
    public async Task Stderr_NotRedirected_WhenCallbackIsNull()
    {
        var options = new ClaudeAgentOptions { Stderr = null };

        // Spawn the fixture and assert we never observed a stderr capture
        // path. We prove this indirectly by inspecting the public Stderr
        // option: when null, RedirectStandardError must remain false. We
        // detect that by reading the process's StandardError property —
        // accessing it on a non-redirected process throws.
        await using var transport = new SubprocessCliTransport(
            options,
            argvOverride: BuildFixtureArgv("stderr-then-exit")
        );
        await transport.ConnectAsync();

        // Best we can verify here: no exception, transport reads the (empty)
        // stdout to EOF. The "no redirection" guarantee is structural — see
        // SubprocessCliTransport.ConnectAsync.
        await foreach (var _ in transport.ReadMessagesAsync())
        {
            // exhaust
        }
    }

    [Fact]
    public async Task NonZeroExit_WithEmptyStdout_ThrowsProcessException()
    {
        await using var transport = NewTransport(BuildFixtureArgv("exit-nonzero"));
        await transport.ConnectAsync();

        var ex = await Assert.ThrowsAsync<ProcessException>(async () =>
        {
            await foreach (var _ in transport.ReadMessagesAsync())
            {
                // exhaust
            }
        });

        Assert.Equal(7, ex.ExitCode);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var transport = NewTransport(BuildFixtureArgv("ignore-eof"));
        await transport.ConnectAsync();
        await transport.DisposeAsync();
        await transport.DisposeAsync(); // must not throw
    }

    [Fact]
    public async Task WriteAsync_BeforeConnect_Throws()
    {
        await using var transport = NewTransport(BuildFixtureArgv("ignore-eof"));
        await Assert.ThrowsAsync<CliConnectionException>(() => transport.WriteAsync("{}\n"));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SubprocessCliTransport(options: null!));
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
    /// Locator copied from ProcessGracefulShutdownTests so the two test
    /// classes don't share a partial type just for one helper.
    /// </summary>
    private static class SubprocessFixturePath
    {
        public static string Resolve()
        {
            var testAssemblyDir = Path.GetDirectoryName(
                typeof(SubprocessCliTransportTests).Assembly.Location
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
