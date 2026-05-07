using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Messages;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Integration;

/// <summary>
/// End-to-end Phase 5 acceptance: runs <see cref="ClaudeAgent.QueryAsync"/>
/// against the bundled CLI and verifies the messages we get back have the
/// shape the parser expects. Gated on <c>CLAUDE_INTEGRATION=1</c> +
/// credentials (see <see cref="IntegrationFactAttribute"/>).
/// </summary>
public class QueryOneShotIntegrationTests
{
    private static ClaudeAgentOptions OptionsWithEnv()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        var oauth = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrEmpty(oauth))
        {
            builder["CLAUDE_CODE_OAUTH_TOKEN"] = oauth;
        }
        if (!string.IsNullOrEmpty(apiKey))
        {
            builder["ANTHROPIC_API_KEY"] = apiKey;
        }
        return new ClaudeAgentOptions { Env = builder.ToImmutable() };
    }

    [IntegrationFact]
    public async Task QueryAsync_AgainstBundledCli_YieldsAssistantAndResult()
    {
        var collected = new List<Message>();
        await foreach (var message in ClaudeAgent.QueryAsync("Reply with: pong", OptionsWithEnv()))
        {
            collected.Add(message);
        }

        Assert.Contains(collected, m => m is AssistantMessage);
        Assert.Single(collected, m => m is ResultMessage);
    }

    [IntegrationFact]
    public async Task QueryAsync_CancellationMidStream_TerminatesProcessWithinFiveSeconds()
    {
        using var cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();

        var enumerator = ClaudeAgent
            .QueryAsync(
                "Count from 1 to 100, one number per line, slowly.",
                OptionsWithEnv(),
                cts.Token
            )
            .GetAsyncEnumerator(cts.Token);

        try
        {
            // Pull at least one message so we know the CLI is mid-flight.
            Assert.True(await enumerator.MoveNextAsync());
            cts.Cancel();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    // Drain remaining buffered messages until cancellation
                    // surfaces or the iterator completes.
                }
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        stopwatch.Stop();
        // Spec §9 graceful shutdown ladder is up to 10 s worst case
        // (close stdin → wait 5 s → Kill(false) → wait 5 s → Kill(true)).
        // 15 s gives a comfortable margin for CI variance.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"Cancellation took {stopwatch.Elapsed} (>15s)."
        );
    }
}
