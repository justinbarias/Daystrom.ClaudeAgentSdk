using System;
using System.Text.Json.Nodes;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

public class UserMessageInputTests
{
    [Fact]
    public void DefaultConstructor_WithObjectInitializer_RoundTripsText()
    {
        var input = new UserMessageInput { Text = "hello", SessionId = "s1" };

        Assert.Equal("hello", input.Text);
        Assert.Equal("s1", input.SessionId);
        Assert.Null(input.ParentToolUseId);

        var node = JsonNode.Parse(input.ToWireJson())!.AsObject();
        Assert.Equal("user", node["type"]!.GetValue<string>());
        Assert.Equal("s1", node["session_id"]!.GetValue<string>());
        Assert.Equal("hello", node["message"]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void Ctor_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UserMessageInput(null!));
    }
}
