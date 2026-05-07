using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Daystrom.ClaudeAgentSdk.Json;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.OutputFormat;
using Daystrom.ClaudeAgentSdk.Sandbox;
using Daystrom.ClaudeAgentSdk.SystemPrompt;
using Daystrom.ClaudeAgentSdk.ThinkingConfig;
using Daystrom.ClaudeAgentSdk.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThinkingConfigType = Daystrom.ClaudeAgentSdk.ThinkingConfig.ThinkingConfig;

namespace Daystrom.ClaudeAgentSdk.Internal;

/// <summary>
/// Translates a <see cref="ClaudeAgentOptions"/> instance into the argv
/// vector passed to the bundled CLI, mirroring the Python SDK's
/// <c>_build_command()</c> in <c>subprocess_cli.py</c> flag-for-flag and
/// in the same order so upstream changes track mechanically.
/// </summary>
/// <remarks>
/// <para>
/// Two output modes:
/// </para>
/// <list type="bullet">
///   <item>
///     <see cref="CommandMode.Streaming"/> — appends
///     <c>--input-format stream-json</c>; mirrors what Python emits.
///     Use whenever the streaming control protocol is needed (hooks,
///     <c>CanUseTool</c>, in-process MCP, custom transports).
///   </item>
///   <item>
///     <see cref="CommandMode.OneShot"/> — appends
///     <c>--print -- "&lt;prompt&gt;"</c> as the final tokens. The .NET
///     SDK's one-shot fast path (spec §9.0).
///   </item>
/// </list>
/// <para>
/// Skills defaults (<c>Skill</c> / <c>Skill(name)</c> tools and
/// <c>setting_sources=[user,project]</c>) and sandbox-merge into
/// <c>--settings</c> JSON match Python identically.
/// </para>
/// <para>
/// All JSON payloads (<c>--mcp-config</c>, <c>--settings</c>,
/// <c>--json-schema</c>) flow through
/// <see cref="ClaudeAgentJsonContext"/> so the builder is AOT-clean.
/// </para>
/// </remarks>
public sealed class CommandBuilder
{
    private readonly string _cliPath;
    private readonly ClaudeAgentOptions _options;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    /// <summary>Creates a builder bound to a specific CLI path and options bag.</summary>
    /// <param name="cliPath">Resolved path to the <c>claude</c> executable.</param>
    /// <param name="options">User-supplied session options.</param>
    /// <param name="fileSystem">
    /// Filesystem abstraction used to read a settings file when merging it
    /// with sandbox JSON. Defaults to <see cref="DefaultFileSystem.Instance"/>.
    /// </param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger"/>.</param>
    public CommandBuilder(
        string cliPath,
        ClaudeAgentOptions options,
        IFileSystem? fileSystem = null,
        ILogger? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(cliPath);
        ArgumentNullException.ThrowIfNull(options);
        _cliPath = cliPath;
        _options = options;
        _fileSystem = fileSystem ?? DefaultFileSystem.Instance;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Builds argv for the streaming control-protocol path.</summary>
    public IReadOnlyList<string> BuildStreaming() => Build(CommandMode.Streaming, prompt: null);

    /// <summary>
    /// Builds argv for the one-shot <c>--print</c> fast path; the prompt
    /// is appended as the final argv token after a literal <c>--</c>.
    /// </summary>
    public IReadOnlyList<string> BuildOneShot(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return Build(CommandMode.OneShot, prompt);
    }

    private List<string> Build(CommandMode mode, string? prompt)
    {
        var cmd = new List<string>(32) { _cliPath, "--output-format", "stream-json", "--verbose" };

        AppendSystemPrompt(cmd);
        AppendTools(cmd);

        var (effectiveAllowedTools, effectiveSettingSources) = ApplySkillsDefaults();
        if (effectiveAllowedTools.Count > 0)
        {
            cmd.Add("--allowedTools");
            cmd.Add(string.Join(",", effectiveAllowedTools));
        }

        if (_options.MaxTurns is { } maxTurns)
        {
            cmd.Add("--max-turns");
            cmd.Add(maxTurns.ToString(CultureInfo.InvariantCulture));
        }

        if (_options.MaxBudgetUsd is { } maxBudget)
        {
            cmd.Add("--max-budget-usd");
            cmd.Add(maxBudget.ToString(CultureInfo.InvariantCulture));
        }

        if (_options.DisallowedTools.Count > 0)
        {
            cmd.Add("--disallowedTools");
            cmd.Add(string.Join(",", _options.DisallowedTools));
        }

        if (_options.TaskBudget is { } tb)
        {
            cmd.Add("--task-budget");
            cmd.Add(tb.Total.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(_options.Model))
        {
            cmd.Add("--model");
            cmd.Add(_options.Model);
        }

        if (!string.IsNullOrEmpty(_options.FallbackModel))
        {
            cmd.Add("--fallback-model");
            cmd.Add(_options.FallbackModel);
        }

        if (_options.Betas.Count > 0)
        {
            cmd.Add("--betas");
            cmd.Add(string.Join(",", _options.Betas.Select(BetaToWire)));
        }

        if (!string.IsNullOrEmpty(_options.PermissionPromptToolName))
        {
            cmd.Add("--permission-prompt-tool");
            cmd.Add(_options.PermissionPromptToolName);
        }

        if (_options.PermissionMode is { } pm)
        {
            cmd.Add("--permission-mode");
            cmd.Add(PermissionModeToWire(pm));
        }

        if (_options.ContinueConversation)
        {
            cmd.Add("--continue");
        }

        if (!string.IsNullOrEmpty(_options.Resume))
        {
            cmd.Add("--resume");
            cmd.Add(_options.Resume);
        }

        if (!string.IsNullOrEmpty(_options.SessionId))
        {
            cmd.Add("--session-id");
            cmd.Add(_options.SessionId);
        }

        var settingsValue = BuildSettingsValue();
        if (settingsValue is not null)
        {
            cmd.Add("--settings");
            cmd.Add(settingsValue);
        }

        foreach (var dir in _options.AddDirs)
        {
            cmd.Add("--add-dir");
            cmd.Add(dir);
        }

        AppendMcpServers(cmd);

        if (_options.IncludePartialMessages)
        {
            cmd.Add("--include-partial-messages");
        }

        if (_options.ForkSession)
        {
            cmd.Add("--fork-session");
        }

        if (_options.SessionStore is not null)
        {
            cmd.Add("--session-mirror");
        }

        if (effectiveSettingSources is not null)
        {
            cmd.Add(
                "--setting-sources="
                    + string.Join(",", effectiveSettingSources.Select(SourceToWire))
            );
        }

        foreach (var plugin in _options.Plugins)
        {
            if (!string.Equals(plugin.Type, "local", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Unsupported plugin type: {plugin.Type}",
                    nameof(_options.Plugins)
                );
            }
            cmd.Add("--plugin-dir");
            cmd.Add(plugin.Path);
        }

        foreach (var (flag, value) in _options.ExtraArgs)
        {
            if (value is null)
            {
                cmd.Add($"--{flag}");
            }
            else
            {
                cmd.Add($"--{flag}");
                cmd.Add(value);
            }
        }

        AppendThinking(cmd);

        if (!string.IsNullOrEmpty(_options.Effort))
        {
            cmd.Add("--effort");
            cmd.Add(_options.Effort);
        }

        if (_options.OutputFormat is OutputFormatJsonSchema schemaFmt)
        {
            cmd.Add("--json-schema");
            cmd.Add(schemaFmt.Schema.GetRawText());
        }

        switch (mode)
        {
            case CommandMode.Streaming:
                cmd.Add("--input-format");
                cmd.Add("stream-json");
                break;
            case CommandMode.OneShot:
                cmd.Add("--print");
                cmd.Add("--");
                cmd.Add(prompt!);
                break;
        }

        return cmd;
    }

    private void AppendSystemPrompt(List<string> cmd)
    {
        switch (_options.SystemPrompt)
        {
            case null:
                cmd.Add("--system-prompt");
                cmd.Add(string.Empty);
                break;
            case SystemPromptText text:
                cmd.Add("--system-prompt");
                cmd.Add(text.Value);
                break;
            case SystemPromptFile file:
                cmd.Add("--system-prompt-file");
                cmd.Add(file.Path);
                break;
            case SystemPromptPreset preset when preset.Append is { } appendText:
                cmd.Add("--append-system-prompt");
                cmd.Add(appendText);
                break;
            case SystemPromptPreset:
                // Bare preset with no append — Python emits nothing here; the
                // CLI defaults to the bundled preset already.
                break;
        }
    }

    private void AppendTools(List<string> cmd)
    {
        if (_options.Tools is null)
        {
            return;
        }
        cmd.Add("--tools");
        cmd.Add(_options.Tools.Count == 0 ? string.Empty : string.Join(",", _options.Tools));
    }

    private (List<string> AllowedTools, List<SettingSource>? SettingSources) ApplySkillsDefaults()
    {
        var allowedTools = new List<string>(_options.AllowedTools);
        List<SettingSource>? settingSources = _options.SettingSources is null
            ? null
            : new List<SettingSource>(_options.SettingSources);

        var skills = _options.Skills;
        if (skills is null)
        {
            return (allowedTools, settingSources);
        }

        // SDK currently models skills as a list. An empty list mirrors
        // Python's "skills with no names" — no tool injection. A non-empty
        // list injects Skill(name) entries. The Python "all" sentinel maps
        // to the AllSkills constant on the .NET side; check for it
        // explicitly so users can opt into the bare Skill tool.
        if (
            skills.Count == 1
            && string.Equals(skills[0], AllSkillsSentinel, StringComparison.Ordinal)
        )
        {
            if (!allowedTools.Contains("Skill", StringComparer.Ordinal))
            {
                allowedTools.Add("Skill");
            }
        }
        else
        {
            foreach (var name in skills)
            {
                var pattern = $"Skill({name})";
                if (!allowedTools.Contains(pattern, StringComparer.Ordinal))
                {
                    allowedTools.Add(pattern);
                }
            }
        }

        settingSources ??= new List<SettingSource> { SettingSource.User, SettingSource.Project };
        return (allowedTools, settingSources);
    }

    private void AppendThinking(List<string> cmd)
    {
        if (_options.Thinking is { } thinking)
        {
            switch (thinking)
            {
                case ThinkingConfigAdaptive adaptive:
                    cmd.Add("--thinking");
                    cmd.Add("adaptive");
                    if (adaptive.Display is { } adisplay)
                    {
                        cmd.Add("--thinking-display");
                        cmd.Add(DisplayToWire(adisplay));
                    }
                    break;
                case ThinkingConfigEnabled enabled:
                    cmd.Add("--max-thinking-tokens");
                    cmd.Add(enabled.BudgetTokens.ToString(CultureInfo.InvariantCulture));
                    if (enabled.Display is { } edisplay)
                    {
                        cmd.Add("--thinking-display");
                        cmd.Add(DisplayToWire(edisplay));
                    }
                    break;
                case ThinkingConfigDisabled:
                    cmd.Add("--thinking");
                    cmd.Add("disabled");
                    break;
            }
            return;
        }

        if (_options.MaxThinkingTokens is { } legacyTokens)
        {
            cmd.Add("--max-thinking-tokens");
            cmd.Add(legacyTokens.ToString(CultureInfo.InvariantCulture));
        }
    }

    private void AppendMcpServers(List<string> cmd)
    {
        if (_options.McpServers is null || _options.McpServers.Count == 0)
        {
            return;
        }

        var dict = new Dictionary<string, McpServerConfig>(
            _options.McpServers,
            StringComparer.Ordinal
        );
        var wrapper = new McpServersWrapper { McpServers = dict };

        var json = JsonSerializer.Serialize(
            wrapper,
            (JsonTypeInfo<McpServersWrapper>)
                ClaudeAgentJsonContext.Default.GetTypeInfo(typeof(McpServersWrapper))!
        );
        cmd.Add("--mcp-config");
        cmd.Add(json);
    }

    [SuppressMessage(
        "Reliability",
        "CA1031:Do not catch general exception types",
        Justification = "Mirrors Python's swallow-and-warn behavior on settings parse failure."
    )]
    private string? BuildSettingsValue()
    {
        var hasSettings = !string.IsNullOrEmpty(_options.Settings);
        var hasSandbox = _options.Sandbox is not null;

        if (!hasSettings && !hasSandbox)
        {
            return null;
        }

        if (hasSettings && !hasSandbox)
        {
            return _options.Settings;
        }

        // Need to merge sandbox into the settings JSON.
        JsonObject settingsObj;
        if (hasSettings)
        {
            var raw = _options.Settings!.Trim();
            if (raw.StartsWith('{') && raw.EndsWith('}'))
            {
                try
                {
                    settingsObj = JsonNode.Parse(raw)?.AsObject() ?? new JsonObject();
                }
                catch (JsonException)
                {
                    settingsObj = ReadSettingsFile(raw);
                }
            }
            else
            {
                settingsObj = ReadSettingsFile(raw);
            }
        }
        else
        {
            settingsObj = new JsonObject();
        }

        // Serialize SandboxSettings via source-gen, then re-parse as JsonNode so
        // it embeds cleanly.
        var sandboxJson = JsonSerializer.Serialize(
            _options.Sandbox!,
            (JsonTypeInfo<SandboxSettings>)
                ClaudeAgentJsonContext.Default.GetTypeInfo(typeof(SandboxSettings))!
        );
        settingsObj["sandbox"] = JsonNode.Parse(sandboxJson);

        return settingsObj.ToJsonString();
    }

    private JsonObject ReadSettingsFile(string path)
    {
        if (!_fileSystem.FileExists(path))
        {
            _logger.LogWarning("Settings file not found: {Path}", path);
            return new JsonObject();
        }

        try
        {
            var contents = _fileSystem.ReadAllText(path);
            return JsonNode.Parse(contents)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse settings file as JSON; ignoring: {Path}", path);
            return new JsonObject();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read settings file: {Path}", path);
            return new JsonObject();
        }
    }

    private const string AllSkillsSentinel = "*";

    /// <summary>Sentinel value for "all skills" in <see cref="ClaudeAgentOptions.Skills"/>.</summary>
    public static string AllSkills => AllSkillsSentinel;

    private static string PermissionModeToWire(PermissionMode mode) =>
        mode switch
        {
            PermissionMode.Default => "default",
            PermissionMode.AcceptEdits => "acceptEdits",
            PermissionMode.BypassPermissions => "bypassPermissions",
            PermissionMode.Plan => "plan",
            PermissionMode.DontAsk => "dontAsk",
            PermissionMode.Auto => "auto",
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unknown PermissionMode"
            ),
        };

    private static string SourceToWire(SettingSource source) =>
        source switch
        {
            SettingSource.User => "user",
            SettingSource.Project => "project",
            SettingSource.Local => "local",
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                "Unknown SettingSource"
            ),
        };

    private static string BetaToWire(SdkBeta beta) =>
        beta switch
        {
            SdkBeta.Context1m20250807 => "context-1m-2025-08-07",
            _ => throw new ArgumentOutOfRangeException(nameof(beta), beta, "Unknown SdkBeta"),
        };

    private static string DisplayToWire(ThinkingDisplay display) =>
        display switch
        {
            ThinkingDisplay.Summarized => "summarized",
            ThinkingDisplay.Omitted => "omitted",
            _ => throw new ArgumentOutOfRangeException(
                nameof(display),
                display,
                "Unknown ThinkingDisplay"
            ),
        };

    private enum CommandMode
    {
        Streaming,
        OneShot,
    }
}

/// <summary>
/// Internal wrapper used to emit the <c>{"mcpServers": {...}}</c> shape
/// expected by the CLI's <c>--mcp-config</c> flag without allocating a
/// reflection path.
/// </summary>
internal sealed record McpServersWrapper
{
    public required IReadOnlyDictionary<string, McpServerConfig> McpServers { get; init; }
}
