using System;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Integration;

/// <summary>
/// xUnit fact that runs only when <c>CLAUDE_INTEGRATION=1</c> is set in
/// the environment <em>and</em> a credential is available
/// (<c>CLAUDE_CODE_OAUTH_TOKEN</c> or <c>ANTHROPIC_API_KEY</c>). Mirrors
/// the Python SDK's <c>pytest.mark.integration</c>.
/// </summary>
/// <remarks>
/// CI runs these only on the Linux leg with a real key plumbed through a
/// repo secret. Local <c>dotnet test</c> skips them silently.
/// </remarks>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CLAUDE_INTEGRATION") != "1")
        {
            Skip = "Set CLAUDE_INTEGRATION=1 to run integration tests.";
            return;
        }

        var hasOauth = !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN")
        );
        var hasApiKey = !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        );
        if (!hasOauth && !hasApiKey)
        {
            Skip = "Integration tests require CLAUDE_CODE_OAUTH_TOKEN or ANTHROPIC_API_KEY.";
        }
    }
}
