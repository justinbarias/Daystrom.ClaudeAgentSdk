using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.ClaudeAgentSdk.ThinkingConfig;
using Xunit;

namespace Anthropic.ClaudeAgentSdk.Tests.ThinkingConfig;

public class ThinkingConfigRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Adaptive_Parses()
    {
        var cfg =
            JsonSerializer.Deserialize<Anthropic.ClaudeAgentSdk.ThinkingConfig.ThinkingConfig>(
                """{"type":"adaptive","display":"summarized"}""",
                Options
            );
        var adaptive = Assert.IsType<ThinkingConfigAdaptive>(cfg);
        Assert.Equal(ThinkingDisplay.Summarized, adaptive.Display);
    }

    [Fact]
    public void Enabled_ParsesBudget()
    {
        var cfg =
            JsonSerializer.Deserialize<Anthropic.ClaudeAgentSdk.ThinkingConfig.ThinkingConfig>(
                """{"type":"enabled","budget_tokens":2000}""",
                Options
            );
        var enabled = Assert.IsType<ThinkingConfigEnabled>(cfg);
        Assert.Equal(2000, enabled.BudgetTokens);
    }

    [Fact]
    public void Disabled_Parses()
    {
        var cfg =
            JsonSerializer.Deserialize<Anthropic.ClaudeAgentSdk.ThinkingConfig.ThinkingConfig>(
                """{"type":"disabled"}""",
                Options
            );
        Assert.IsType<ThinkingConfigDisabled>(cfg);
    }
}
