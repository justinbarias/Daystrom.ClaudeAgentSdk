using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk;
using Daystrom.ClaudeAgentSdk.Agents;
using Daystrom.ClaudeAgentSdk.Hooks;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.OutputFormat;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Sandbox;
using Daystrom.ClaudeAgentSdk.Sessions;
using Daystrom.ClaudeAgentSdk.SystemPrompt;
using Daystrom.ClaudeAgentSdk.ThinkingConfig;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

public class ClaudeAgentOptionsBuilderTests
{
    [Fact]
    public void DefaultBuild_HasEmptyCollectionsAndNullScalars()
    {
        var options = ClaudeAgentOptions.Create().Build();

        Assert.Null(options.Model);
        Assert.Null(options.FallbackModel);
        Assert.Empty(options.AllowedTools);
        Assert.Empty(options.DisallowedTools);
        Assert.Null(options.Tools);
        Assert.Null(options.Skills);
        Assert.Null(options.PermissionMode);
        Assert.Null(options.PermissionPromptToolName);
        Assert.Null(options.CanUseTool);
        Assert.Null(options.Hooks);
        Assert.Null(options.McpServers);
        Assert.Empty(options.Plugins);
        Assert.Null(options.Agents);
        Assert.Null(options.Thinking);
        Assert.Null(options.MaxThinkingTokens);
        Assert.Null(options.Effort);
        Assert.Null(options.MaxTurns);
        Assert.Null(options.MaxBudgetUsd);
        Assert.Null(options.TaskBudget);
        Assert.Null(options.Resume);
        Assert.False(options.ContinueConversation);
        Assert.Null(options.SessionId);
        Assert.False(options.ForkSession);
        Assert.Null(options.SessionStore);
        Assert.False(options.IncludePartialMessages);
        Assert.Null(options.Settings);
        Assert.Null(options.Sandbox);
        Assert.Null(options.SystemPrompt);
        Assert.Null(options.SettingSources);
        Assert.Empty(options.AddDirs);
        Assert.Empty(options.Env);
        Assert.Null(options.Cwd);
        Assert.Null(options.User);
        Assert.Null(options.CliPath);
        Assert.Null(options.Stderr);
        Assert.Empty(options.Betas);
        Assert.Null(options.OutputFormat);
        Assert.Empty(options.ExtraArgs);
        Assert.Null(options.MaxBufferSize);
        Assert.False(options.EnableFileCheckpointing);
    }

    [Fact]
    public void WithModel_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithModel("claude-sonnet-4-6").Build();
        Assert.Equal("claude-sonnet-4-6", options.Model);
    }

    [Fact]
    public void WithFallbackModel_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithFallbackModel("claude-haiku-4-5").Build();
        Assert.Equal("claude-haiku-4-5", options.FallbackModel);
    }

    [Fact]
    public void WithAllowedTools_AppendsAcrossCalls()
    {
        var options = ClaudeAgentOptions
            .Create()
            .WithAllowedTools("Bash", "Read")
            .WithAllowedTools("Edit")
            .Build();
        Assert.Equal(new[] { "Bash", "Read", "Edit" }, options.AllowedTools);
    }

    [Fact]
    public void WithDisallowedTools_AppendsAcrossCalls()
    {
        var options = ClaudeAgentOptions
            .Create()
            .WithDisallowedTools("WebFetch")
            .WithDisallowedTools("WebSearch")
            .Build();
        Assert.Equal(new[] { "WebFetch", "WebSearch" }, options.DisallowedTools);
    }

    [Fact]
    public void WithTools_EmptyList_DisablesBuiltInTools()
    {
        var options = ClaudeAgentOptions.Create().WithTools().Build();
        Assert.NotNull(options.Tools);
        Assert.Empty(options.Tools!);
    }

    [Fact]
    public void WithSkills_EmptyList_DistinctFromUnsetNull()
    {
        var unset = ClaudeAgentOptions.Create().Build();
        var explicitlyEmpty = ClaudeAgentOptions.Create().WithSkills().Build();

        Assert.Null(unset.Skills);
        Assert.NotNull(explicitlyEmpty.Skills);
        Assert.Empty(explicitlyEmpty.Skills!);
    }

    [Fact]
    public void WithPermissionMode_RoundTrips()
    {
        var options = ClaudeAgentOptions
            .Create()
            .WithPermissionMode(PermissionMode.AcceptEdits)
            .Build();
        Assert.Equal(PermissionMode.AcceptEdits, options.PermissionMode);
    }

    [Fact]
    public void WithPermissionPromptToolName_RoundTrips()
    {
        var options = ClaudeAgentOptions
            .Create()
            .WithPermissionPromptToolName("custom_prompt")
            .Build();
        Assert.Equal("custom_prompt", options.PermissionPromptToolName);
    }

    [Fact]
    public void OnCanUseTool_RoundTrips()
    {
        CanUseToolDelegate handler = (_, _, _, _) =>
            new ValueTask<PermissionResult>(new PermissionResultAllow());
        var options = ClaudeAgentOptions.Create().OnCanUseTool(handler).Build();
        Assert.Same(handler, options.CanUseTool);
    }

    [Fact]
    public void OnHook_AccumulatesPerEvent()
    {
        var matcherA = new HookMatcher { Matcher = "Bash" };
        var matcherB = new HookMatcher { Matcher = "Read" };
        var options = ClaudeAgentOptions
            .Create()
            .OnHook(HookEvent.PreToolUse, matcherA)
            .OnHook(HookEvent.PreToolUse, matcherB)
            .Build();
        Assert.NotNull(options.Hooks);
        Assert.Equal(new[] { matcherA, matcherB }, options.Hooks![HookEvent.PreToolUse]);
    }

    [Fact]
    public void WithMcpServer_StoresInDictionary()
    {
        var stdio = new McpStdioServerConfig { Command = "node" };
        var options = ClaudeAgentOptions.Create().WithMcpServer("local", stdio).Build();
        Assert.NotNull(options.McpServers);
        Assert.Same(stdio, options.McpServers!["local"]);
    }

    [Fact]
    public void WithPlugin_AppendsAcrossCalls()
    {
        var p1 = new Daystrom.ClaudeAgentSdk.Agents.Plugins.SdkPluginConfig
        {
            Type = "local",
            Path = "./plugin-a",
        };
        var p2 = new Daystrom.ClaudeAgentSdk.Agents.Plugins.SdkPluginConfig
        {
            Type = "local",
            Path = "./plugin-b",
        };
        var options = ClaudeAgentOptions.Create().WithPlugin(p1).WithPlugin(p2).Build();
        Assert.Equal(new[] { p1, p2 }, options.Plugins);
    }

    [Fact]
    public void WithAgent_StoresInDictionary()
    {
        var agent = new AgentDefinition { Description = "researcher", Prompt = "do research" };
        var options = ClaudeAgentOptions.Create().WithAgent("research", agent).Build();
        Assert.NotNull(options.Agents);
        Assert.Same(agent, options.Agents!["research"]);
    }

    [Fact]
    public void WithThinking_RoundTrips()
    {
        var thinking = new ThinkingConfigEnabled { BudgetTokens = 4096 };
        var options = ClaudeAgentOptions.Create().WithThinking(thinking).Build();
        Assert.Same(thinking, options.Thinking);
    }

    [Fact]
    public void WithMaxThinkingTokens_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithMaxThinkingTokens(2048).Build();
        Assert.Equal(2048, options.MaxThinkingTokens);
    }

    [Fact]
    public void WithEffort_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithEffort("high").Build();
        Assert.Equal("high", options.Effort);
    }

    [Fact]
    public void WithMaxTurns_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithMaxTurns(7).Build();
        Assert.Equal(7, options.MaxTurns);
    }

    [Fact]
    public void WithMaxBudgetUsd_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithMaxBudgetUsd(2.5m).Build();
        Assert.Equal(2.5m, options.MaxBudgetUsd);
    }

    [Fact]
    public void WithTaskBudget_RoundTrips()
    {
        var budget = new TaskBudget { Total = 50_000 };
        var options = ClaudeAgentOptions.Create().WithTaskBudget(budget).Build();
        Assert.Same(budget, options.TaskBudget);
    }

    [Fact]
    public void ResumeSession_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().ResumeSession("sess-123").Build();
        Assert.Equal("sess-123", options.Resume);
    }

    [Fact]
    public void ContinueConversation_FlipsFlag()
    {
        var options = ClaudeAgentOptions.Create().ContinueConversation().Build();
        Assert.True(options.ContinueConversation);
    }

    [Fact]
    public void WithSessionId_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithSessionId("uuid-abc").Build();
        Assert.Equal("uuid-abc", options.SessionId);
    }

    [Fact]
    public void ForkSession_FlipsFlag()
    {
        var options = ClaudeAgentOptions.Create().ForkSession().Build();
        Assert.True(options.ForkSession);
    }

    [Fact]
    public void WithSessionStore_RoundTrips()
    {
        var store = new NoopSessionStore();
        var options = ClaudeAgentOptions.Create().WithSessionStore(store).Build();
        Assert.Same(store, options.SessionStore);
    }

    [Fact]
    public void IncludePartialMessages_FlipsFlag()
    {
        var options = ClaudeAgentOptions.Create().IncludePartialMessages().Build();
        Assert.True(options.IncludePartialMessages);
    }

    [Fact]
    public void WithSettings_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithSettings("/tmp/settings.json").Build();
        Assert.Equal("/tmp/settings.json", options.Settings);
    }

    [Fact]
    public void WithSandbox_RoundTrips()
    {
        var sandbox = new SandboxSettings { Enabled = true };
        var options = ClaudeAgentOptions.Create().WithSandbox(sandbox).Build();
        Assert.Same(sandbox, options.Sandbox);
    }

    [Fact]
    public void WithSystemPrompt_PlainString_WrapsInText()
    {
        var options = ClaudeAgentOptions.Create().WithSystemPrompt("you are helpful").Build();
        var text = Assert.IsType<SystemPromptText>(options.SystemPrompt);
        Assert.Equal("you are helpful", text.Value);
    }

    [Fact]
    public void WithSystemPrompt_TypedSpec_RoundTrips()
    {
        var spec = new SystemPromptPreset(Append: "extra", ExcludeDynamicSections: true);
        var options = ClaudeAgentOptions.Create().WithSystemPrompt(spec).Build();
        Assert.Same(spec, options.SystemPrompt);
    }

    [Fact]
    public void WithSettingSources_EmptyList_DistinctFromUnsetNull()
    {
        var unset = ClaudeAgentOptions.Create().Build();
        var disabled = ClaudeAgentOptions.Create().WithSettingSources().Build();

        Assert.Null(unset.SettingSources);
        Assert.NotNull(disabled.SettingSources);
        Assert.Empty(disabled.SettingSources!);
    }

    [Fact]
    public void WithSettingSources_RoundTripsExplicitList()
    {
        var options = ClaudeAgentOptions
            .Create()
            .WithSettingSources(SettingSource.User, SettingSource.Project)
            .Build();
        Assert.Equal(new[] { SettingSource.User, SettingSource.Project }, options.SettingSources);
    }

    [Fact]
    public void WithAddDirs_AppendsAcrossCalls()
    {
        var options = ClaudeAgentOptions.Create().WithAddDirs("/a").WithAddDirs("/b", "/c").Build();
        Assert.Equal(new[] { "/a", "/b", "/c" }, options.AddDirs);
    }

    [Fact]
    public void WithEnv_AccumulatesEntries()
    {
        var options = ClaudeAgentOptions.Create().WithEnv("FOO", "1").WithEnv("BAR", "2").Build();
        Assert.Equal("1", options.Env["FOO"]);
        Assert.Equal("2", options.Env["BAR"]);
    }

    [Fact]
    public void WithCwd_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithCwd("/work").Build();
        Assert.Equal("/work", options.Cwd);
    }

    [Fact]
    public void WithUser_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithUser("alice").Build();
        Assert.Equal("alice", options.User);
    }

    [Fact]
    public void WithCliPath_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithCliPath("/usr/local/bin/claude").Build();
        Assert.Equal("/usr/local/bin/claude", options.CliPath);
    }

    [Fact]
    public void OnStderr_RoundTrips()
    {
        Action<string> handler = _ => { };
        var options = ClaudeAgentOptions.Create().OnStderr(handler).Build();
        Assert.Same(handler, options.Stderr);
    }

    [Fact]
    public void WithBetas_AppendsAcrossCalls()
    {
        var options = ClaudeAgentOptions
            .Create()
            .WithBetas(SdkBeta.Context1m20250807)
            .WithBetas(SdkBeta.Context1m20250807)
            .Build();
        Assert.Equal(2, options.Betas.Count);
    }

    [Fact]
    public void WithOutputFormat_RoundTrips()
    {
        using var doc = JsonDocument.Parse("{\"type\":\"object\"}");
        var format = new OutputFormatJsonSchema(doc.RootElement.Clone());
        var options = ClaudeAgentOptions.Create().WithOutputFormat(format).Build();
        Assert.Same(format, options.OutputFormat);
    }

    [Fact]
    public void WithExtraArg_AccumulatesEntries()
    {
        var options = ClaudeAgentOptions
            .Create()
            .WithExtraArg("verbose", null)
            .WithExtraArg("output", "json")
            .Build();
        Assert.Null(options.ExtraArgs["verbose"]);
        Assert.Equal("json", options.ExtraArgs["output"]);
    }

    [Fact]
    public void WithMaxBufferSize_RoundTrips()
    {
        var options = ClaudeAgentOptions.Create().WithMaxBufferSize(2 * 1024 * 1024).Build();
        Assert.Equal(2 * 1024 * 1024, options.MaxBufferSize);
    }

    [Fact]
    public void EnableFileCheckpointing_FlipsFlag()
    {
        var options = ClaudeAgentOptions.Create().EnableFileCheckpointing().Build();
        Assert.True(options.EnableFileCheckpointing);
    }

    [Fact]
    public void RecordWithExpression_RoundTripsEveryField()
    {
        // Acceptance: a fully-populated options instance round-trips through
        // a `with` expression unchanged. This guards against init-only fields
        // being silently dropped if someone reorders the record.
        var thinking = new ThinkingConfigAdaptive();
        var sandbox = new SandboxSettings { Enabled = true };
        var sessionStore = new NoopSessionStore();
        var stdio = new McpStdioServerConfig { Command = "node" };
        var matcher = new HookMatcher { Matcher = "Bash" };
        var systemPrompt = new SystemPromptFile("/tmp/prompt.md");
        using var doc = JsonDocument.Parse("{\"k\":1}");
        var output = new OutputFormatJsonSchema(doc.RootElement.Clone());
        var agent = new AgentDefinition { Description = "d", Prompt = "p" };
        var plugin = new Daystrom.ClaudeAgentSdk.Agents.Plugins.SdkPluginConfig
        {
            Type = "local",
            Path = "./plug",
        };
        var taskBudget = new TaskBudget { Total = 1000 };
        Action<string> stderr = _ => { };
        CanUseToolDelegate canUse = (_, _, _, _) =>
            new ValueTask<PermissionResult>(new PermissionResultAllow());

        var original = ClaudeAgentOptions
            .Create()
            .WithModel("opus")
            .WithFallbackModel("haiku")
            .WithAllowedTools("Bash")
            .WithDisallowedTools("WebSearch")
            .WithTools("Read", "Edit")
            .WithSkills("a", "b")
            .WithPermissionMode(PermissionMode.Plan)
            .WithPermissionPromptToolName("ask")
            .OnCanUseTool(canUse)
            .OnHook(HookEvent.PreToolUse, matcher)
            .WithMcpServer("s", stdio)
            .WithPlugin(plugin)
            .WithAgent("r", agent)
            .WithThinking(thinking)
            .WithMaxThinkingTokens(0)
            .WithEffort("max")
            .WithMaxTurns(3)
            .WithMaxBudgetUsd(1m)
            .WithTaskBudget(taskBudget)
            .ResumeSession("sid")
            .ContinueConversation()
            .WithSessionId("uuid")
            .ForkSession()
            .WithSessionStore(sessionStore)
            .IncludePartialMessages()
            .WithSettings("/s")
            .WithSandbox(sandbox)
            .WithSystemPrompt(systemPrompt)
            .WithSettingSources(SettingSource.Local)
            .WithAddDirs("/d")
            .WithEnv("E", "1")
            .WithCwd("/c")
            .WithUser("u")
            .WithCliPath("/cli")
            .OnStderr(stderr)
            .WithBetas(SdkBeta.Context1m20250807)
            .WithOutputFormat(output)
            .WithExtraArg("flag", null)
            .WithMaxBufferSize(123)
            .EnableFileCheckpointing()
            .Build();

        var copy = original with { };

        Assert.Equal(original, copy);
        Assert.Equal(original.Model, copy.Model);
        Assert.Equal(original.AllowedTools, copy.AllowedTools);
        Assert.Equal(original.Hooks?.Keys.ToArray(), copy.Hooks?.Keys.ToArray());
        Assert.Equal(original.SystemPrompt, copy.SystemPrompt);
        Assert.Same(original.Stderr, copy.Stderr);
    }

    private sealed class NoopSessionStore : ISessionStore
    {
        public Task AppendAsync(
            SessionKey key,
            System.Collections.Generic.IReadOnlyList<SessionStoreEntry> entries,
            System.Threading.CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task<System.Collections.Generic.IReadOnlyList<SessionStoreEntry>?> LoadAsync(
            SessionKey key,
            System.Threading.CancellationToken cancellationToken
        ) => Task.FromResult<System.Collections.Generic.IReadOnlyList<SessionStoreEntry>?>(null);
    }
}
