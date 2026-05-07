using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.ClaudeAgentSdk.Permissions;
using Xunit;

namespace Anthropic.ClaudeAgentSdk.Tests.Permissions;

public class PermissionResultRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Allow_ParsesViaBehaviorDiscriminator()
    {
        var json = """{"behavior":"allow"}""";
        var result = JsonSerializer.Deserialize<PermissionResult>(json, Options);
        Assert.IsType<PermissionResultAllow>(result);
    }

    [Fact]
    public void Deny_ParsesAndPreservesFields()
    {
        var json = """{"behavior":"deny","message":"nope","interrupt":true}""";
        var result = JsonSerializer.Deserialize<PermissionResult>(json, Options);
        var deny = Assert.IsType<PermissionResultDeny>(result);
        Assert.Equal("nope", deny.Message);
        Assert.True(deny.Interrupt);
    }

    [Fact]
    public void Allow_DiscriminatorSerializesAsBehavior_NotType()
    {
        var json = JsonSerializer.Serialize<PermissionResult>(new PermissionResultAllow(), Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("allow", doc.RootElement.GetProperty("behavior").GetString());
        Assert.False(doc.RootElement.TryGetProperty("type", out _));
    }
}
