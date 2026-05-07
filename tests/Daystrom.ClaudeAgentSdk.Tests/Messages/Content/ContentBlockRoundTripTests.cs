using System.Text.Json;
using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Messages.Content;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Messages.Content;

/// <summary>
/// Round-trips every <see cref="ContentBlock"/> variant through STJ against
/// fixtures captured from the Python SDK (paired with CLI 2.1.126).
/// </summary>
public class ContentBlockRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void TextBlock_Parses()
    {
        var json = Fixtures.Read("content/text.json");
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        var text = Assert.IsType<TextBlock>(block);
        Assert.Equal("Hello", text.Text);
    }

    [Fact]
    public void ThinkingBlock_Parses()
    {
        var json = Fixtures.Read("content/thinking.json");
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        var thinking = Assert.IsType<ThinkingBlock>(block);
        Assert.Equal("I'm thinking about the answer...", thinking.Thinking);
        Assert.Equal("sig-123", thinking.Signature);
    }

    [Fact]
    public void ToolUseBlock_Parses()
    {
        var json = Fixtures.Read("content/tool_use.json");
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        var use = Assert.IsType<ToolUseBlock>(block);
        Assert.Equal("tool_456", use.Id);
        Assert.Equal("Read", use.Name);
        Assert.Equal("/example.txt", use.Input.GetProperty("file_path").GetString());
    }

    [Fact]
    public void ToolResultBlock_Parses_StringContent()
    {
        var json = Fixtures.Read("content/tool_result.json");
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        var result = Assert.IsType<ToolResultBlock>(block);
        Assert.Equal("tool_789", result.ToolUseId);
        Assert.NotNull(result.Content);
        Assert.Equal("File contents here", result.Content!.Value.GetString());
        Assert.Null(result.IsError);
    }

    [Fact]
    public void ToolResultBlock_Parses_WithIsError()
    {
        var json = Fixtures.Read("content/tool_result_error.json");
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        var result = Assert.IsType<ToolResultBlock>(block);
        Assert.Equal("tool_error", result.ToolUseId);
        Assert.True(result.IsError);
    }

    [Fact]
    public void ServerToolUseBlock_Parses()
    {
        var json = Fixtures.Read("content/server_tool_use.json");
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        var use = Assert.IsType<ServerToolUseBlock>(block);
        Assert.Equal("srvtoolu_01ABC", use.Id);
        Assert.Equal(ServerToolName.Advisor, use.Name);
    }

    [Fact]
    public void ServerToolResultBlock_Parses_FromAdvisorWireString()
    {
        var json = Fixtures.Read("content/server_tool_result.json");
        var block = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        var result = Assert.IsType<ServerToolResultBlock>(block);
        Assert.Equal("srvtoolu_01ABC", result.ToolUseId);
        Assert.Equal("advisor_result", result.Content.GetProperty("type").GetString());
    }

    [Fact]
    public void RoundTrip_Preserves_TextBlock()
    {
        var original = new TextBlock("Hello");
        var json = JsonSerializer.Serialize<ContentBlock>(original, Options);
        var roundTripped = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void RoundTrip_Preserves_ThinkingBlock()
    {
        var original = new ThinkingBlock("musing", "sig");
        var json = JsonSerializer.Serialize<ContentBlock>(original, Options);
        var roundTripped = JsonSerializer.Deserialize<ContentBlock>(json, Options);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Discriminator_Text_AppearsAsTypeField()
    {
        var json = JsonSerializer.Serialize<ContentBlock>(new TextBlock("hi"), Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("text", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void Discriminator_ServerToolResult_SerializesAsAdvisorToolResult()
    {
        var json = JsonSerializer.Serialize<ContentBlock>(
            new ServerToolResultBlock
            {
                ToolUseId = "srv_01",
                Content = JsonSerializer.SerializeToElement(new { type = "x" }, Options),
            },
            Options
        );
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("advisor_tool_result", doc.RootElement.GetProperty("type").GetString());
    }
}
