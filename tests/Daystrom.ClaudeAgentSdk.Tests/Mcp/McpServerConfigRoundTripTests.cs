using System.Text.Json;
using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Mcp;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Mcp;

public class McpServerConfigRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void StdioConfig_DispatchesViaTypeDiscriminator()
    {
        var json = """{"type":"stdio","command":"node","args":["server.js"]}""";
        var cfg = JsonSerializer.Deserialize<McpServerConfig>(json, Options);
        var stdio = Assert.IsType<McpStdioServerConfig>(cfg);
        Assert.Equal("node", stdio.Command);
        Assert.NotNull(stdio.Args);
        Assert.Equal("server.js", stdio.Args![0]);
    }

    [Fact]
    public void HttpConfig_Parses()
    {
        var json = """{"type":"http","url":"https://x.example/mcp"}""";
        var cfg = JsonSerializer.Deserialize<McpServerConfig>(json, Options);
        var http = Assert.IsType<McpHttpServerConfig>(cfg);
        Assert.Equal("https://x.example/mcp", http.Url);
    }

    [Fact]
    public void SseConfig_Parses()
    {
        var json = """{"type":"sse","url":"https://x.example/sse"}""";
        var cfg = JsonSerializer.Deserialize<McpServerConfig>(json, Options);
        var sse = Assert.IsType<McpSseServerConfig>(cfg);
        Assert.Equal("https://x.example/sse", sse.Url);
    }

    [Fact]
    public void StatusConfig_ClaudeAIProxy_Parses()
    {
        var json = """{"type":"claudeai-proxy","url":"https://claude.ai/proxy","id":"p1"}""";
        var cfg = JsonSerializer.Deserialize<McpServerStatusConfig>(json, Options);
        var proxy = Assert.IsType<McpClaudeAIProxyServerConfig>(cfg);
        Assert.Equal("p1", proxy.Id);
    }
}
