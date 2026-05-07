using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.ClaudeAgentSdk;
using Anthropic.ClaudeAgentSdk.Hooks;
using Anthropic.ClaudeAgentSdk.Mcp;
using Anthropic.ClaudeAgentSdk.Messages;
using Anthropic.ClaudeAgentSdk.Messages.Content;
using Anthropic.ClaudeAgentSdk.Permissions;
using Anthropic.ClaudeAgentSdk.Sessions;
using Anthropic.ClaudeAgentSdk.ThinkingConfig;
using Xunit;

namespace Anthropic.ClaudeAgentSdk.Tests;

/// <summary>
/// Round-trips every wire enum through <c>JsonSerializer</c> with the
/// <c>JsonStringEnumConverter</c> active, asserting (a) every member maps
/// to the wire string declared in <c>Fixtures/discriminators.md</c>, and
/// (b) the inverse parse recovers the same value. Catches casing drift
/// (snake_case vs camelCase vs kebab-case vs PascalCase) the moment a
/// new member is added.
/// </summary>
/// <remarks>
/// Uses an ad-hoc <c>JsonSerializerOptions</c> rather than the source-gen
/// context (which lands in Phase 3.8). The source-gen path will run the
/// same assertions as part of its own test suite.
/// </remarks>
public class EnumRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options).Trim('"');

    private static T Deserialize<T>(string wire) =>
        JsonSerializer.Deserialize<T>($"\"{wire}\"", Options)!;

    [Theory]
    [InlineData(PermissionMode.Default, "default")]
    [InlineData(PermissionMode.AcceptEdits, "acceptEdits")]
    [InlineData(PermissionMode.BypassPermissions, "bypassPermissions")]
    [InlineData(PermissionMode.Plan, "plan")]
    [InlineData(PermissionMode.DontAsk, "dontAsk")]
    [InlineData(PermissionMode.Auto, "auto")]
    public void PermissionMode_RoundTrips(PermissionMode value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<PermissionMode>(wire));
    }

    [Theory]
    [InlineData(SettingSource.User, "user")]
    [InlineData(SettingSource.Project, "project")]
    [InlineData(SettingSource.Local, "local")]
    public void SettingSource_RoundTrips(SettingSource value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<SettingSource>(wire));
    }

    [Theory]
    [InlineData(SdkBeta.Context1m20250807, "context-1m-2025-08-07")]
    public void SdkBeta_RoundTrips(SdkBeta value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<SdkBeta>(wire));
    }

    [Theory]
    [InlineData(RateLimitStatus.Allowed, "allowed")]
    [InlineData(RateLimitStatus.AllowedWarning, "allowed_warning")]
    [InlineData(RateLimitStatus.Rejected, "rejected")]
    public void RateLimitStatus_RoundTrips(RateLimitStatus value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<RateLimitStatus>(wire));
    }

    [Theory]
    [InlineData(RateLimitType.FiveHour, "five_hour")]
    [InlineData(RateLimitType.SevenDay, "seven_day")]
    [InlineData(RateLimitType.SevenDayOpus, "seven_day_opus")]
    [InlineData(RateLimitType.SevenDaySonnet, "seven_day_sonnet")]
    [InlineData(RateLimitType.Overage, "overage")]
    public void RateLimitType_RoundTrips(RateLimitType value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<RateLimitType>(wire));
    }

    [Theory]
    [InlineData(HookEvent.PreToolUse, "PreToolUse")]
    [InlineData(HookEvent.PostToolUse, "PostToolUse")]
    [InlineData(HookEvent.PostToolUseFailure, "PostToolUseFailure")]
    [InlineData(HookEvent.UserPromptSubmit, "UserPromptSubmit")]
    [InlineData(HookEvent.Stop, "Stop")]
    [InlineData(HookEvent.SubagentStop, "SubagentStop")]
    [InlineData(HookEvent.SubagentStart, "SubagentStart")]
    [InlineData(HookEvent.PreCompact, "PreCompact")]
    [InlineData(HookEvent.Notification, "Notification")]
    [InlineData(HookEvent.PermissionRequest, "PermissionRequest")]
    public void HookEvent_RoundTrips(HookEvent value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<HookEvent>(wire));
    }

    [Theory]
    [InlineData(ServerToolName.Advisor, "advisor")]
    [InlineData(ServerToolName.WebSearch, "web_search")]
    [InlineData(ServerToolName.WebFetch, "web_fetch")]
    [InlineData(ServerToolName.CodeExecution, "code_execution")]
    [InlineData(ServerToolName.BashCodeExecution, "bash_code_execution")]
    [InlineData(ServerToolName.TextEditorCodeExecution, "text_editor_code_execution")]
    [InlineData(ServerToolName.ToolSearchToolRegex, "tool_search_tool_regex")]
    [InlineData(ServerToolName.ToolSearchToolBm25, "tool_search_tool_bm25")]
    public void ServerToolName_RoundTrips(ServerToolName value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<ServerToolName>(wire));
    }

    [Theory]
    [InlineData(McpServerConnectionStatus.Connected, "connected")]
    [InlineData(McpServerConnectionStatus.Failed, "failed")]
    [InlineData(McpServerConnectionStatus.NeedsAuth, "needs-auth")]
    [InlineData(McpServerConnectionStatus.Pending, "pending")]
    [InlineData(McpServerConnectionStatus.Disabled, "disabled")]
    public void McpServerConnectionStatus_RoundTrips(McpServerConnectionStatus value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<McpServerConnectionStatus>(wire));
    }

    [Theory]
    [InlineData(TaskNotificationStatus.Completed, "completed")]
    [InlineData(TaskNotificationStatus.Failed, "failed")]
    [InlineData(TaskNotificationStatus.Stopped, "stopped")]
    public void TaskNotificationStatus_RoundTrips(TaskNotificationStatus value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<TaskNotificationStatus>(wire));
    }

    [Theory]
    [InlineData(SessionStoreFlushMode.Batched, "batched")]
    [InlineData(SessionStoreFlushMode.Eager, "eager")]
    public void SessionStoreFlushMode_RoundTrips(SessionStoreFlushMode value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<SessionStoreFlushMode>(wire));
    }

    [Theory]
    [InlineData(PermissionBehavior.Allow, "allow")]
    [InlineData(PermissionBehavior.Deny, "deny")]
    [InlineData(PermissionBehavior.Ask, "ask")]
    public void PermissionBehavior_RoundTrips(PermissionBehavior value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<PermissionBehavior>(wire));
    }

    [Theory]
    [InlineData(AssistantMessageError.AuthenticationFailed, "authentication_failed")]
    [InlineData(AssistantMessageError.BillingError, "billing_error")]
    [InlineData(AssistantMessageError.RateLimit, "rate_limit")]
    [InlineData(AssistantMessageError.InvalidRequest, "invalid_request")]
    [InlineData(AssistantMessageError.ServerError, "server_error")]
    [InlineData(AssistantMessageError.Unknown, "unknown")]
    public void AssistantMessageError_RoundTrips(AssistantMessageError value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<AssistantMessageError>(wire));
    }

    [Theory]
    [InlineData(ThinkingDisplay.Summarized, "summarized")]
    [InlineData(ThinkingDisplay.Omitted, "omitted")]
    public void ThinkingDisplay_RoundTrips(ThinkingDisplay value, string wire)
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<ThinkingDisplay>(wire));
    }

    [Theory]
    [InlineData(PermissionUpdateDestination.UserSettings, "userSettings")]
    [InlineData(PermissionUpdateDestination.ProjectSettings, "projectSettings")]
    [InlineData(PermissionUpdateDestination.LocalSettings, "localSettings")]
    [InlineData(PermissionUpdateDestination.Session, "session")]
    public void PermissionUpdateDestination_RoundTrips(
        PermissionUpdateDestination value,
        string wire
    )
    {
        Assert.Equal(wire, Serialize(value));
        Assert.Equal(value, Deserialize<PermissionUpdateDestination>(wire));
    }
}
