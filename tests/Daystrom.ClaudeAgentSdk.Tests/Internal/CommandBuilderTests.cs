using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daystrom.ClaudeAgentSdk;
using Daystrom.ClaudeAgentSdk.Agents.Plugins;
using Daystrom.ClaudeAgentSdk.Internal;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.OutputFormat;
using Daystrom.ClaudeAgentSdk.Sandbox;
using Daystrom.ClaudeAgentSdk.SystemPrompt;
using Daystrom.ClaudeAgentSdk.ThinkingConfig;
using Daystrom.ClaudeAgentSdk.Transport;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Internal;

public class CommandBuilderTests
{
    private const string FakeCli = "/usr/local/bin/claude";

    [Fact]
    public void BuildStreaming_DefaultOptions_EmitsBaselineFlags()
    {
        var options = new ClaudeAgentOptions();
        var argv = new CommandBuilder(FakeCli, options).BuildStreaming();

        Assert.Equal(
            new[]
            {
                FakeCli,
                "--output-format",
                "stream-json",
                "--verbose",
                "--system-prompt",
                "",
                "--input-format",
                "stream-json",
            },
            argv
        );
    }

    [Fact]
    public void BuildOneShot_EndsWithDoubleDashAndPrompt()
    {
        var options = new ClaudeAgentOptions();
        var argv = new CommandBuilder(FakeCli, options).BuildOneShot("hello world");

        Assert.Equal("--print", argv[^3]);
        Assert.Equal("--", argv[^2]);
        Assert.Equal("hello world", argv[^1]);
        Assert.DoesNotContain("--input-format", argv);
    }

    [Fact]
    public void SystemPrompt_TextVariant_EmitsSystemPromptFlag()
    {
        var options = new ClaudeAgentOptions { SystemPrompt = SystemPromptSpec.Text("be helpful") };
        var argv = new CommandBuilder(FakeCli, options).BuildStreaming();

        AssertContainsPair(argv, "--system-prompt", "be helpful");
        Assert.DoesNotContain("--system-prompt-file", argv);
        Assert.DoesNotContain("--append-system-prompt", argv);
    }

    [Fact]
    public void SystemPrompt_FileVariant_EmitsFileFlag()
    {
        var options = new ClaudeAgentOptions { SystemPrompt = new SystemPromptFile("/tmp/sp.md") };
        var argv = new CommandBuilder(FakeCli, options).BuildStreaming();

        AssertContainsPair(argv, "--system-prompt-file", "/tmp/sp.md");
    }

    [Fact]
    public void SystemPrompt_PresetWithAppend_EmitsAppendFlag()
    {
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new SystemPromptPreset(Append: "extra note"),
        };
        var argv = new CommandBuilder(FakeCli, options).BuildStreaming();

        AssertContainsPair(argv, "--append-system-prompt", "extra note");
    }

    [Fact]
    public void SystemPrompt_BarePreset_EmitsNothingExtra()
    {
        var options = new ClaudeAgentOptions { SystemPrompt = new SystemPromptPreset() };
        var argv = new CommandBuilder(FakeCli, options).BuildStreaming();

        Assert.DoesNotContain("--system-prompt", argv);
        Assert.DoesNotContain("--append-system-prompt", argv);
        Assert.DoesNotContain("--system-prompt-file", argv);
    }

    [Fact]
    public void Tools_NullStaysOff_EmptyEmitsEmptyString_NonEmptyJoinsCommas()
    {
        var none = new CommandBuilder(FakeCli, new ClaudeAgentOptions()).BuildStreaming();
        Assert.DoesNotContain("--tools", none);

        var empty = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Tools = Array.Empty<string>() }
        ).BuildStreaming();
        AssertContainsPair(empty, "--tools", "");

        var listed = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Tools = new[] { "Read", "Bash" } }
        ).BuildStreaming();
        AssertContainsPair(listed, "--tools", "Read,Bash");
    }

    [Fact]
    public void AllowedAndDisallowedTools_Join_AsCommaSeparated()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                AllowedTools = new[] { "Read", "Glob" },
                DisallowedTools = new[] { "Bash", "Write" },
            }
        ).BuildStreaming();

        AssertContainsPair(argv, "--allowedTools", "Read,Glob");
        AssertContainsPair(argv, "--disallowedTools", "Bash,Write");
    }

    [Fact]
    public void Skills_AllSentinel_InjectsBareSkillTool_AndDefaultsSettingSources()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Skills = new[] { CommandBuilder.AllSkills } }
        ).BuildStreaming();

        AssertContainsPair(argv, "--allowedTools", "Skill");
        Assert.Contains("--setting-sources=user,project", argv);
    }

    [Fact]
    public void Skills_NamedList_InjectsSkillNamePatterns()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Skills = new[] { "alpha", "beta" } }
        ).BuildStreaming();

        AssertContainsPair(argv, "--allowedTools", "Skill(alpha),Skill(beta)");
        Assert.Contains("--setting-sources=user,project", argv);
    }

    [Fact]
    public void Skills_MergedWithExistingAllowedTools_DoesNotDuplicate()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                AllowedTools = new[] { "Skill" },
                Skills = new[] { CommandBuilder.AllSkills },
            }
        ).BuildStreaming();

        AssertContainsPair(argv, "--allowedTools", "Skill");
    }

    [Fact]
    public void Skills_DoesNotOverwriteExplicitSettingSources()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                Skills = new[] { "alpha" },
                SettingSources = new[] { SettingSource.Local },
            }
        ).BuildStreaming();

        Assert.Contains("--setting-sources=local", argv);
    }

    [Fact]
    public void SettingSources_EmptyList_EmitsEmptyValue()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { SettingSources = Array.Empty<SettingSource>() }
        ).BuildStreaming();

        Assert.Contains("--setting-sources=", argv);
    }

    [Fact]
    public void NumericOptions_AreInvariantCultureFormatted()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                MaxTurns = 5,
                MaxBudgetUsd = 1.5m,
                TaskBudget = new TaskBudget { Total = 1000 },
            }
        ).BuildStreaming();

        AssertContainsPair(argv, "--max-turns", "5");
        AssertContainsPair(argv, "--max-budget-usd", "1.5");
        AssertContainsPair(argv, "--task-budget", "1000");
    }

    [Fact]
    public void PermissionMode_MapsToCamelCaseStrings()
    {
        var modes = new[]
        {
            (PermissionMode.Default, "default"),
            (PermissionMode.AcceptEdits, "acceptEdits"),
            (PermissionMode.BypassPermissions, "bypassPermissions"),
            (PermissionMode.Plan, "plan"),
            (PermissionMode.DontAsk, "dontAsk"),
            (PermissionMode.Auto, "auto"),
        };

        foreach (var (mode, wire) in modes)
        {
            var argv = new CommandBuilder(
                FakeCli,
                new ClaudeAgentOptions { PermissionMode = mode }
            ).BuildStreaming();
            AssertContainsPair(argv, "--permission-mode", wire);
        }
    }

    [Fact]
    public void ContinueAndResumeAndSessionId_Emit()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                ContinueConversation = true,
                Resume = "abc",
                SessionId = "session-1",
            }
        ).BuildStreaming();

        Assert.Contains("--continue", argv);
        AssertContainsPair(argv, "--resume", "abc");
        AssertContainsPair(argv, "--session-id", "session-1");
    }

    [Fact]
    public void IncludePartial_ForkSession_AndSessionMirror_EmitFlagsOnly()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                IncludePartialMessages = true,
                ForkSession = true,
                SessionStore = new NoopSessionStore(),
            }
        ).BuildStreaming();

        Assert.Contains("--include-partial-messages", argv);
        Assert.Contains("--fork-session", argv);
        Assert.Contains("--session-mirror", argv);
    }

    [Fact]
    public void AddDirs_EmitsOneFlagPerDirectory()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { AddDirs = new[] { "/a", "/b" } }
        ).BuildStreaming();

        var flags = argv.Select((v, i) => (v, i)).Where(t => t.v == "--add-dir").ToList();
        Assert.Equal(2, flags.Count);
        Assert.Equal("/a", argv[flags[0].i + 1]);
        Assert.Equal("/b", argv[flags[1].i + 1]);
    }

    [Fact]
    public void Plugins_LocalType_EmitOnePluginDirPerEntry()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                Plugins = new[]
                {
                    new SdkPluginConfig { Type = "local", Path = "/plugins/foo" },
                    new SdkPluginConfig { Type = "local", Path = "/plugins/bar" },
                },
            }
        ).BuildStreaming();

        var flags = argv.Select((v, i) => (v, i)).Where(t => t.v == "--plugin-dir").ToList();
        Assert.Equal(2, flags.Count);
        Assert.Equal("/plugins/foo", argv[flags[0].i + 1]);
        Assert.Equal("/plugins/bar", argv[flags[1].i + 1]);
    }

    [Fact]
    public void Plugins_NonLocalType_Throws()
    {
        var builder = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                Plugins = new[]
                {
                    new SdkPluginConfig { Type = "remote", Path = "/x" },
                },
            }
        );

        Assert.Throws<ArgumentException>(() => builder.BuildStreaming());
    }

    [Fact]
    public void ExtraArgs_NullValue_BecomesBareFlag_ValueBecomesPair()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                ExtraArgs = new Dictionary<string, string?>
                {
                    ["debug-foo"] = null,
                    ["bar"] = "42",
                },
            }
        ).BuildStreaming();

        Assert.Contains("--debug-foo", argv);
        AssertContainsPair(argv, "--bar", "42");
    }

    [Fact]
    public void Thinking_AdaptiveWithDisplay_EmitsThinkingAndDisplay()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                Thinking = new ThinkingConfigAdaptive { Display = ThinkingDisplay.Summarized },
            }
        ).BuildStreaming();

        AssertContainsPair(argv, "--thinking", "adaptive");
        AssertContainsPair(argv, "--thinking-display", "summarized");
    }

    [Fact]
    public void Thinking_EnabledWithBudget_EmitsMaxTokens()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Thinking = new ThinkingConfigEnabled { BudgetTokens = 8000 } }
        ).BuildStreaming();

        AssertContainsPair(argv, "--max-thinking-tokens", "8000");
        Assert.DoesNotContain("--thinking-display", argv);
    }

    [Fact]
    public void Thinking_DisabledVariant_EmitsThinkingDisabled()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Thinking = new ThinkingConfigDisabled() }
        ).BuildStreaming();

        AssertContainsPair(argv, "--thinking", "disabled");
    }

    [Fact]
    public void Thinking_LegacyMaxTokens_UsedWhenThinkingNull()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { MaxThinkingTokens = 4000 }
        ).BuildStreaming();

        AssertContainsPair(argv, "--max-thinking-tokens", "4000");
    }

    [Fact]
    public void Thinking_NewConfigTakesPrecedenceOverLegacy()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                Thinking = new ThinkingConfigDisabled(),
                MaxThinkingTokens = 9999,
            }
        ).BuildStreaming();

        AssertContainsPair(argv, "--thinking", "disabled");
        Assert.DoesNotContain("--max-thinking-tokens", argv);
    }

    [Fact]
    public void OutputFormat_JsonSchema_EmitsJsonSchemaFlag()
    {
        var schema = JsonDocument
            .Parse("""{"type":"object","properties":{"x":{"type":"number"}}}""")
            .RootElement;
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { OutputFormat = new OutputFormatJsonSchema(schema) }
        ).BuildStreaming();

        var idx = IndexOf(argv, "--json-schema");
        Assert.True(idx >= 0);
        var emittedJson = JsonNode.Parse(argv[idx + 1])!;
        Assert.Equal("object", emittedJson["type"]!.GetValue<string>());
    }

    [Fact]
    public void Settings_PathOnly_PassesThroughVerbatim()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Settings = "/path/to/settings.json" }
        ).BuildStreaming();

        AssertContainsPair(argv, "--settings", "/path/to/settings.json");
    }

    [Fact]
    public void Settings_JsonStringOnly_PassesThroughVerbatim()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Settings = """{"foo":1}""" }
        ).BuildStreaming();

        AssertContainsPair(argv, "--settings", """{"foo":1}""");
    }

    [Fact]
    public void Settings_SandboxOnly_EmitsObjectWithSandboxKey()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Sandbox = new SandboxSettings { Enabled = true } }
        ).BuildStreaming();

        var idx = IndexOf(argv, "--settings");
        Assert.True(idx >= 0);
        var node = JsonNode.Parse(argv[idx + 1])!.AsObject();
        Assert.True(node.ContainsKey("sandbox"));
        Assert.True(node["sandbox"]!.AsObject()["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Settings_JsonStringPlusSandbox_MergesUnderSandboxKey()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                Settings = """{"foo":"bar"}""",
                Sandbox = new SandboxSettings { Enabled = false },
            }
        ).BuildStreaming();

        var idx = IndexOf(argv, "--settings");
        var merged = JsonNode.Parse(argv[idx + 1])!.AsObject();
        Assert.Equal("bar", merged["foo"]!.GetValue<string>());
        Assert.False(merged["sandbox"]!.AsObject()["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Settings_PathPlusSandbox_ReadsFileAndMerges()
    {
        var fs = new RecordingFileSystem();
        fs.AddFile("/etc/settings.json", """{"baseline":42}""");

        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions
            {
                Settings = "/etc/settings.json",
                Sandbox = new SandboxSettings { Enabled = true },
            },
            fileSystem: fs
        ).BuildStreaming();

        var idx = IndexOf(argv, "--settings");
        var merged = JsonNode.Parse(argv[idx + 1])!.AsObject();
        Assert.Equal(42, merged["baseline"]!.GetValue<int>());
        Assert.True(merged["sandbox"]!.AsObject()["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void McpServers_EmitsMcpConfigJson()
    {
        var servers = new Dictionary<string, McpServerConfig>
        {
            ["fs"] = new McpStdioServerConfig
            {
                Command = "/usr/bin/fs-mcp",
                Args = new[] { "--root", "/tmp" },
            },
        };
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { McpServers = servers }
        ).BuildStreaming();

        var idx = IndexOf(argv, "--mcp-config");
        Assert.True(idx >= 0);
        var node = JsonNode.Parse(argv[idx + 1])!.AsObject();
        Assert.True(node.ContainsKey("mcp_servers") || node.ContainsKey("mcpServers"));
    }

    [Fact]
    public void Betas_JoinsCommaSeparated()
    {
        var argv = new CommandBuilder(
            FakeCli,
            new ClaudeAgentOptions { Betas = new[] { SdkBeta.Context1m20250807 } }
        ).BuildStreaming();

        AssertContainsPair(argv, "--betas", "context-1m-2025-08-07");
    }

    [Fact]
    public void NullCliPath_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandBuilder(string.Empty, new ClaudeAgentOptions())
        );
    }

    private static int IndexOf(IReadOnlyList<string> argv, string token)
    {
        for (var i = 0; i < argv.Count; i++)
        {
            if (argv[i] == token)
            {
                return i;
            }
        }
        return -1;
    }

    private static void AssertContainsPair(IReadOnlyList<string> argv, string flag, string value)
    {
        for (var i = 0; i < argv.Count - 1; i++)
        {
            if (argv[i] == flag && argv[i + 1] == value)
            {
                return;
            }
        }
        Assert.Fail($"Expected pair [{flag} {value}] not found in argv: {string.Join(' ', argv)}");
    }

    private sealed class NoopSessionStore : Daystrom.ClaudeAgentSdk.Sessions.ISessionStore
    {
        public System.Threading.Tasks.Task AppendAsync(
            Daystrom.ClaudeAgentSdk.Sessions.SessionKey key,
            IReadOnlyList<Daystrom.ClaudeAgentSdk.Sessions.SessionStoreEntry> entries,
            System.Threading.CancellationToken cancellationToken
        ) => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<IReadOnlyList<Daystrom.ClaudeAgentSdk.Sessions.SessionStoreEntry>?> LoadAsync(
            Daystrom.ClaudeAgentSdk.Sessions.SessionKey key,
            System.Threading.CancellationToken cancellationToken
        ) =>
            System.Threading.Tasks.Task.FromResult<IReadOnlyList<Daystrom.ClaudeAgentSdk.Sessions.SessionStoreEntry>?>(
                null
            );
    }

    private sealed class RecordingFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public void AddFile(string path, string contents) => _files[path] = contents;

        public bool FileExists(string path) => _files.ContainsKey(path);

        public string? GetEnvironmentVariable(string name) => null;

        public string GetUserHome() => "/home/test";

        public string ReadAllText(string path) =>
            _files.TryGetValue(path, out var v)
                ? v
                : throw new System.IO.FileNotFoundException(path);
    }
}
