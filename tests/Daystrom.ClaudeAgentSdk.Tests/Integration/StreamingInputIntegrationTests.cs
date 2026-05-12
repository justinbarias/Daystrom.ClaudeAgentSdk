using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Permissions;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Integration;

/// <summary>
/// Phase 6 acceptance for the streaming-input
/// <see cref="ClaudeAgent.QueryAsync(System.Collections.Generic.IAsyncEnumerable{UserMessageInput}, ClaudeAgentOptions?, CancellationToken)"/>
/// overload: pump three user messages through the bundled CLI and observe
/// three assistant + result pairs. Gated on <c>CLAUDE_INTEGRATION=1</c> +
/// credentials.
/// </summary>
public class StreamingInputIntegrationTests
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
        // Force the streaming control-protocol path. CanUseTool that always
        // allows is the simplest gate-true trigger.
        return new ClaudeAgentOptions
        {
            Env = builder.ToImmutable(),
            CanUseTool = (_, _, _, _) =>
                ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
        };
    }

    private static async IAsyncEnumerable<UserMessageInput> FromChannelAsync(
        ChannelReader<UserMessageInput> reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    [IntegrationFact]
    public async Task ThreeTurnStreamingInput_AgainstBundledCli_ObservesThreeAssistantResultPairs()
    {
        var prompts = new[]
        {
            "Reply with the single word: one",
            "Reply with the single word: two",
            "Reply with the single word: three",
        };
        var inputs = Channel.CreateUnbounded<UserMessageInput>();

        // Producer: enqueue prompts, but only after each prompt's result
        // arrives so the CLI sees a clean turn boundary. We coordinate via
        // a SemaphoreSlim.
        using var nextTurn = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        var assistantTurns = 0;
        var resultTurns = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var producer = Task.Run(async () =>
        {
            foreach (var p in prompts)
            {
                await nextTurn.WaitAsync(cts.Token);
                await inputs.Writer.WriteAsync(new UserMessageInput(p), cts.Token);
            }
            // Wait for the final turn to release the semaphore before we
            // close the channel — closing it signals EndInputAsync so the
            // CLI can exit.
            await nextTurn.WaitAsync(cts.Token);
            inputs.Writer.Complete();
        });

        await foreach (
            var message in ClaudeAgent.QueryAsync(
                FromChannelAsync(inputs.Reader, cts.Token),
                OptionsWithEnv(),
                cts.Token
            )
        )
        {
            if (message is AssistantMessage)
            {
                assistantTurns++;
            }
            if (message is ResultMessage)
            {
                resultTurns++;
                nextTurn.Release();
            }
        }

        await producer;

        Assert.True(
            assistantTurns >= 3,
            $"Expected at least 3 assistant turns across three user messages, saw {assistantTurns}."
        );
        Assert.Equal(3, resultTurns);
    }
}
