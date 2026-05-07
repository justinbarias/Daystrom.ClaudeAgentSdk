using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Daystrom.ClaudeAgentSdk;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Messages.Content;

namespace QuickStart;

/// <summary>
/// Phase 5 demo: runs a single one-shot query against the bundled CLI and
/// prints each <see cref="Message"/> the SDK yields. Reads credentials
/// from the host environment (CLAUDE_CODE_OAUTH_TOKEN takes precedence
/// over ANTHROPIC_API_KEY) and forwards whichever is set into the child
/// process via <see cref="ClaudeAgentOptions.Env"/>.
/// </summary>
internal static class Program
{
    private const string OAuthEnvKey = "CLAUDE_CODE_OAUTH_TOKEN";
    private const string ApiKeyEnvKey = "ANTHROPIC_API_KEY";

    private static async System.Threading.Tasks.Task<int> Main(string[] args)
    {
        var prompt = args.Length > 0 ? string.Join(' ', args) : "Say hello in one short sentence.";

        if (!TryBuildEnv(out var env, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var options = new ClaudeAgentOptions { Env = env };

        await foreach (var message in ClaudeAgent.QueryAsync(prompt, options))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (var block in assistant.Message.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Console.WriteLine(text.Text);
                        }
                    }
                    break;
                case ResultMessage result:
                    Console.WriteLine(
                        $"-- done ({result.NumTurns} turn(s), {result.DurationMs} ms)"
                    );
                    break;
            }
        }

        return 0;
    }

    private static bool TryBuildEnv(out IReadOnlyDictionary<string, string> env, out string? error)
    {
        var oauth = Environment.GetEnvironmentVariable(OAuthEnvKey);
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvKey);

        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        if (!string.IsNullOrEmpty(oauth))
        {
            builder[OAuthEnvKey] = oauth;
        }
        if (!string.IsNullOrEmpty(apiKey))
        {
            builder[ApiKeyEnvKey] = apiKey;
        }

        if (builder.Count == 0)
        {
            env = ImmutableDictionary<string, string>.Empty;
            error =
                $"Set one of {OAuthEnvKey} or {ApiKeyEnvKey} in the host environment "
                + "before running QuickStart.";
            return false;
        }

        env = builder.ToImmutable();
        error = null;
        return true;
    }
}
