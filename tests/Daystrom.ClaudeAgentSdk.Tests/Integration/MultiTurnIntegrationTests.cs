using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Messages;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Integration;

/// <summary>
/// Phase 6 acceptance: drive <see cref="ClaudeAgentClient"/> against the
/// bundled CLI through two user turns and through an interrupt mid-turn.
/// Gated on <c>CLAUDE_INTEGRATION=1</c> + credentials (see
/// <see cref="IntegrationFactAttribute"/>).
/// </summary>
public class MultiTurnIntegrationTests
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
    public async Task MultiTurn_SendTwoUserMessages_ObservesTwoAssistantTurns()
    {
        await using var client = ClaudeAgentClient.Create(OptionsWithEnv());
        await client.ConnectAsync();

        var assistantTurns = 0;

        async Task DrainOneTurnAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await foreach (var message in client.ReceiveMessagesAsync(cts.Token))
            {
                if (message is AssistantMessage)
                {
                    assistantTurns++;
                }
                if (message is ResultMessage)
                {
                    return;
                }
            }
        }

        await client.SendUserMessageAsync("Reply with the single word: one");
        await DrainOneTurnAsync();

        await client.SendUserMessageAsync("Reply with the single word: two");
        await DrainOneTurnAsync();

        Assert.True(
            assistantTurns >= 2,
            $"Expected at least 2 assistant turns across two user messages, saw {assistantTurns}."
        );

        await client.DisconnectAsync();
    }

    [IntegrationFact]
    public async Task InterruptMidTurn_ReturnsResultWithinBudget()
    {
        await using var client = ClaudeAgentClient.Create(OptionsWithEnv());
        await client.ConnectAsync();

        await client.SendUserMessageAsync("Count from 1 to 1000, slowly, one number per line.");

        var sawAssistant = false;
        var stopwatch = Stopwatch.StartNew();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var message in client.ReceiveMessagesAsync(cts.Token))
        {
            if (message is AssistantMessage && !sawAssistant)
            {
                sawAssistant = true;
                await client.InterruptAsync();
            }
            if (message is ResultMessage)
            {
                break;
            }
        }

        stopwatch.Stop();
        Assert.True(sawAssistant, "Never observed an AssistantMessage to interrupt against.");
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"Interrupt took {stopwatch.Elapsed} (>15s)."
        );

        await client.DisconnectAsync();
    }
}
