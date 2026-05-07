using System.Text.Json;
using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Hooks;
using Daystrom.ClaudeAgentSdk.Hooks.Inputs;
using Daystrom.ClaudeAgentSdk.Hooks.Outputs;
using Daystrom.ClaudeAgentSdk.Json;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Messages.Content;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Sessions;
using Xunit;
// Disambiguates from the sibling Tests.ThinkingConfig folder/namespace.
using ThinkingConfigBase = Daystrom.ClaudeAgentSdk.ThinkingConfig.ThinkingConfig;
using ThinkingConfigEnabled = Daystrom.ClaudeAgentSdk.ThinkingConfig.ThinkingConfigEnabled;

namespace Daystrom.ClaudeAgentSdk.Tests.Json;

/// <summary>
/// Verifies that <see cref="ClaudeAgentJsonContext"/> round-trips every
/// polymorphic union the SDK serialises across the wire, that its output
/// matches the runtime <see cref="JsonSerializerOptions"/> configuration
/// it claims to embed, and that the <c>WhenWritingNull</c> ignore-policy
/// is actually honoured.
/// </summary>
/// <remarks>
/// The other round-trip suites in this project exercise STJ's
/// reflection-based path with ad-hoc <see cref="JsonSerializerOptions"/>;
/// this suite exclusively uses <c>ClaudeAgentJsonContext.Default.*</c>
/// so AOT-relevant behaviour is regression-tested at PR time, not just
/// when the AotSmoke harness is published manually.
/// </remarks>
public class ClaudeAgentJsonContextTests
{
    private static readonly ClaudeAgentJsonContext Context = ClaudeAgentJsonContext.Default;

    // ── Per-union round-trips through the source-gen context ───────────

    [Fact]
    public void Message_RoundTripsViaContext()
    {
        var msg = new SystemMessage { Subtype = "info" };
        var json = JsonSerializer.Serialize<Message>(msg, Context.Message);
        var back = JsonSerializer.Deserialize(json, Context.Message);
        var sm = Assert.IsType<SystemMessage>(back);
        Assert.Equal("info", sm.Subtype);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("system", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void ContentBlock_RoundTripsViaContext()
    {
        var block = new TextBlock("hello");
        var json = JsonSerializer.Serialize<ContentBlock>(block, Context.ContentBlock);
        var back = JsonSerializer.Deserialize(json, Context.ContentBlock);
        var tb = Assert.IsType<TextBlock>(back);
        Assert.Equal("hello", tb.Text);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("text", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void HookInput_RoundTripsViaContext()
    {
        var input = new PreToolUseHookInput
        {
            SessionId = "s",
            TranscriptPath = "/t",
            Cwd = "/c",
            ToolName = "Bash",
            ToolInput = JsonSerializer.SerializeToElement(new { command = "ls" }),
            ToolUseId = "tu-1",
        };
        var json = JsonSerializer.Serialize<HookInput>(input, Context.HookInput);
        var back = JsonSerializer.Deserialize(json, Context.HookInput);
        var pre = Assert.IsType<PreToolUseHookInput>(back);
        Assert.Equal("Bash", pre.ToolName);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("PreToolUse", doc.RootElement.GetProperty("hook_event_name").GetString());
    }

    [Fact]
    public void HookSpecificOutput_RoundTripsViaContext()
    {
        var output = new NotificationHookSpecificOutput { AdditionalContext = "fyi" };
        var json = JsonSerializer.Serialize<HookSpecificOutput>(output, Context.HookSpecificOutput);
        var back = JsonSerializer.Deserialize(json, Context.HookSpecificOutput);
        var n = Assert.IsType<NotificationHookSpecificOutput>(back);
        Assert.Equal("fyi", n.AdditionalContext);
        using var doc = JsonDocument.Parse(json);
        // Output side uses camelCase hookEventName (not snake_case) — this
        // asymmetry is documented in discriminators.md and verified again
        // here against the context.
        Assert.Equal("Notification", doc.RootElement.GetProperty("hookEventName").GetString());
    }

    [Fact]
    public void McpServerConfig_RoundTripsViaContext()
    {
        var cfg = new McpStdioServerConfig { Command = "/usr/bin/echo" };
        var json = JsonSerializer.Serialize<McpServerConfig>(cfg, Context.McpServerConfig);
        var back = JsonSerializer.Deserialize(json, Context.McpServerConfig);
        var stdio = Assert.IsType<McpStdioServerConfig>(back);
        Assert.Equal("/usr/bin/echo", stdio.Command);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("stdio", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void McpServerStatusConfig_RoundTripsViaContext()
    {
        var cfg = new McpStdioServerStatusConfig { Command = "/usr/bin/echo" };
        var json = JsonSerializer.Serialize<McpServerStatusConfig>(
            cfg,
            Context.McpServerStatusConfig
        );
        var back = JsonSerializer.Deserialize(json, Context.McpServerStatusConfig);
        var stdio = Assert.IsType<McpStdioServerStatusConfig>(back);
        Assert.Equal("/usr/bin/echo", stdio.Command);
    }

    [Fact]
    public void ThinkingConfig_RoundTripsViaContext()
    {
        var cfg = new ThinkingConfigEnabled { BudgetTokens = 8192 };
        // The source-gen emits the property under the type's simple name
        // (ThinkingConfig), not the [JsonSerializable] alias used at the
        // declaration site.
        var json = JsonSerializer.Serialize<ThinkingConfigBase>(cfg, Context.ThinkingConfig);
        var back = JsonSerializer.Deserialize(json, Context.ThinkingConfig);
        var enabled = Assert.IsType<ThinkingConfigEnabled>(back);
        Assert.Equal(8192, enabled.BudgetTokens);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("enabled", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void PermissionResult_RoundTripsViaContext()
    {
        var deny = new PermissionResultDeny { Message = "nope", Interrupt = true };
        var json = JsonSerializer.Serialize<PermissionResult>(deny, Context.PermissionResult);
        var back = JsonSerializer.Deserialize(json, Context.PermissionResult);
        var d = Assert.IsType<PermissionResultDeny>(back);
        Assert.Equal("nope", d.Message);
        Assert.True(d.Interrupt);
        using var doc = JsonDocument.Parse(json);
        // Discriminator property is "behavior", not "type" — captured
        // here so a future refactor doesn't silently break the wire.
        Assert.Equal("deny", doc.RootElement.GetProperty("behavior").GetString());
    }

    // ── Parity vs runtime JsonSerializerOptions ────────────────────────

    [Fact]
    public void Context_OutputMatches_RuntimeOptionsOutput_ForFlatRecord()
    {
        // SessionKey is a flat, non-polymorphic record with snake_case
        // wire field names — the simplest possible parity case. If this
        // ever drifts the context's [JsonSourceGenerationOptions] no
        // longer matches the policy used elsewhere in the codebase.
        var key = new SessionKey
        {
            ProjectKey = "p",
            SessionId = "s",
            Subpath = "subagents/agent-1",
        };

        var contextJson = JsonSerializer.Serialize(key, Context.SessionKey);

        var runtimeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var runtimeJson = JsonSerializer.Serialize(key, runtimeOptions);

        Assert.Equal(runtimeJson, contextJson);
    }

    // ── WhenWritingNull regression guard ───────────────────────────────

    [Fact]
    public void Context_OmitsNullFields_PerWhenWritingNullPolicy()
    {
        // SessionKey.Subpath and SDKSessionInfo's optional fields are
        // null here. The context's DefaultIgnoreCondition = WhenWritingNull
        // must drop them; if the [JsonSourceGenerationOptions] attribute
        // is ever changed, this test is the canary.
        var key = new SessionKey { ProjectKey = "p", SessionId = "s" };
        var keyJson = JsonSerializer.Serialize(key, Context.SessionKey);
        using var keyDoc = JsonDocument.Parse(keyJson);
        Assert.False(
            keyDoc.RootElement.TryGetProperty("subpath", out _),
            "Subpath was null; context must omit it (WhenWritingNull)."
        );

        var info = new SDKSessionInfo
        {
            SessionId = "s",
            Summary = "sum",
            LastModified = 1L,
        };
        var infoJson = JsonSerializer.Serialize(info, Context.SDKSessionInfo);
        using var infoDoc = JsonDocument.Parse(infoJson);
        Assert.False(
            infoDoc.RootElement.TryGetProperty("file_size", out _),
            "FileSize was null; context must omit it (WhenWritingNull)."
        );
        Assert.False(infoDoc.RootElement.TryGetProperty("custom_title", out _));
        Assert.False(infoDoc.RootElement.TryGetProperty("first_prompt", out _));
    }

    // ── HookJsonOutput keyword-field round-trip via context ────────────

    [Fact]
    public void HookJsonOutput_PreservesKeywordFieldsViaContext()
    {
        // `async` and `continue` are C# keywords; HookJsonOutput uses
        // [JsonPropertyName] to map them. Round-trip through the context
        // confirms the source-gen honours the per-property overrides.
        var output = new HookJsonOutput
        {
            Async = true,
            Continue = false,
            Decision = "block",
        };
        var json = JsonSerializer.Serialize(output, Context.HookJsonOutput);
        Assert.Contains("\"async\":true", json);
        Assert.Contains("\"continue\":false", json);
        var back = JsonSerializer.Deserialize(json, Context.HookJsonOutput);
        Assert.NotNull(back);
        Assert.True(back!.Async);
        Assert.False(back.Continue);
        Assert.Equal("block", back.Decision);
    }
}
