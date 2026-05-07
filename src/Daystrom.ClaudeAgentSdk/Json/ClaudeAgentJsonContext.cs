using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Agents;
using Daystrom.ClaudeAgentSdk.Agents.Plugins;
using Daystrom.ClaudeAgentSdk.Hooks;
using Daystrom.ClaudeAgentSdk.Hooks.Inputs;
using Daystrom.ClaudeAgentSdk.Hooks.Outputs;
using Daystrom.ClaudeAgentSdk.Internal;
using Daystrom.ClaudeAgentSdk.Mcp;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Messages.Content;
using Daystrom.ClaudeAgentSdk.Permissions;
using Daystrom.ClaudeAgentSdk.Sandbox;
using Daystrom.ClaudeAgentSdk.Sessions;
using ThinkingConfigType = Daystrom.ClaudeAgentSdk.ThinkingConfig.ThinkingConfig;
using ThinkingDisplayType = Daystrom.ClaudeAgentSdk.ThinkingConfig.ThinkingDisplay;

namespace Daystrom.ClaudeAgentSdk.Json;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> covering every wire
/// type the SDK serialises to or deserialises from the CLI's
/// <c>stream-json</c> NDJSON. Single source of truth for AOT-safe
/// (de)serialisation — no other code path may construct
/// <see cref="System.Text.Json.JsonSerializerOptions"/> directly.
/// </summary>
/// <remarks>
/// <para>
/// Naming: snake-case lower for property names, with per-property
/// <see cref="JsonPropertyNameAttribute"/> overrides for keyword conflicts
/// (<c>async</c>, <c>continue</c>) and the few CLI fields that are
/// camelCase on the wire (<c>resetsAt</c>, <c>hookEventName</c> on outputs,
/// etc.).
/// </para>
/// <para>
/// Enums round-trip as strings via <see cref="JsonStringEnumConverter"/>
/// (set on the source-gen options below). Per-member casing comes from
/// <c>[JsonStringEnumMemberName]</c> on the enum itself.
/// </para>
/// <para>
/// Polymorphic unions (<see cref="Message"/>, <see cref="ContentBlock"/>,
/// <see cref="HookInput"/>, <see cref="HookSpecificOutput"/>,
/// <see cref="McpServerConfig"/>, <see cref="McpServerStatusConfig"/>,
/// <c>ThinkingConfig</c>, <see cref="PermissionResult"/>) carry their
/// <c>[JsonPolymorphic]</c> + <c>[JsonDerivedType]</c> attributes on the
/// base record itself; registering the base here picks up every derived
/// variant automatically.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
// ── Errors ──────────────────────────────────────────────────────────────
// (Exception types do not cross the wire; intentionally omitted.)
//
// ── Enums ───────────────────────────────────────────────────────────────
[JsonSerializable(typeof(PermissionMode))]
[JsonSerializable(typeof(SettingSource))]
[JsonSerializable(typeof(SdkBeta))]
[JsonSerializable(typeof(RateLimitType))]
[JsonSerializable(typeof(RateLimitStatus))]
[JsonSerializable(typeof(ContextUsageCategory))]
[JsonSerializable(typeof(SessionStoreFlushMode))]
[JsonSerializable(typeof(HookEvent))]
[JsonSerializable(typeof(TaskNotificationStatus))]
[JsonSerializable(typeof(ServerToolName))]
[JsonSerializable(typeof(McpServerConnectionStatus))]
[JsonSerializable(typeof(McpServerStatus))]
[JsonSerializable(typeof(PermissionBehavior))]
[JsonSerializable(typeof(PermissionUpdateDestination))]
[JsonSerializable(typeof(ThinkingDisplayType))]
[JsonSerializable(typeof(SandboxIgnoreViolations))]
//
// ── Content blocks (polymorphic on "type") ──────────────────────────────
[JsonSerializable(typeof(ContentBlock))]
[JsonSerializable(typeof(System.Collections.Generic.IReadOnlyList<ContentBlock>))]
//
// ── Top-level Messages (polymorphic on "type") ──────────────────────────
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(AssistantMessage))]
[JsonSerializable(typeof(UserMessage))]
[JsonSerializable(typeof(SystemMessage))]
[JsonSerializable(typeof(ResultMessage))]
[JsonSerializable(typeof(StreamEvent))]
[JsonSerializable(typeof(RateLimitEvent))]
[JsonSerializable(typeof(RateLimitInfo))]
[JsonSerializable(typeof(TaskStartedMessage))]
[JsonSerializable(typeof(TaskProgressMessage))]
[JsonSerializable(typeof(TaskNotificationMessage))]
[JsonSerializable(typeof(TaskBudget))]
[JsonSerializable(typeof(TaskUsage))]
[JsonSerializable(typeof(MirrorErrorMessage))]
[JsonSerializable(typeof(AssistantMessageError))]
[JsonSerializable(typeof(ContextUsageResponse))]
//
// ── Hooks (polymorphic on "hook_event_name" / "hookEventName") ──────────
[JsonSerializable(typeof(HookInput))]
[JsonSerializable(typeof(HookSpecificOutput))]
[JsonSerializable(typeof(HookJsonOutput))]
[JsonSerializable(typeof(HookMatcher))]
//
// ── Permissions (polymorphic on "behavior") ─────────────────────────────
[JsonSerializable(typeof(PermissionResult))]
[JsonSerializable(typeof(PermissionResultAllow))]
[JsonSerializable(typeof(PermissionResultDeny))]
[JsonSerializable(typeof(PermissionUpdate))]
[JsonSerializable(typeof(PermissionRuleValue))]
[JsonSerializable(typeof(ToolPermissionContext))]
//
// ── MCP (polymorphic on "type") ─────────────────────────────────────────
[JsonSerializable(typeof(McpServerConfig))]
[JsonSerializable(typeof(McpServersWrapper))]
[JsonSerializable(typeof(McpServerStatusConfig))]
[JsonSerializable(typeof(McpStatusResponse))]
[JsonSerializable(typeof(McpServerInfo))]
[JsonSerializable(typeof(McpToolInfo))]
[JsonSerializable(typeof(McpToolAnnotations))]
//
// ── Thinking (polymorphic on "type") ────────────────────────────────────
[JsonSerializable(typeof(ThinkingConfigType))]
//
// ── Sandbox ─────────────────────────────────────────────────────────────
[JsonSerializable(typeof(SandboxSettings))]
[JsonSerializable(typeof(SandboxNetworkConfig))]
//
// ── Agents / Plugins ────────────────────────────────────────────────────
[JsonSerializable(typeof(AgentDefinition))]
[JsonSerializable(typeof(SdkPluginConfig))]
//
// ── Sessions ────────────────────────────────────────────────────────────
[JsonSerializable(typeof(SessionKey))]
[JsonSerializable(typeof(SessionStoreEntry))]
[JsonSerializable(typeof(SessionStoreListEntry))]
[JsonSerializable(typeof(SessionListSubkeysKey))]
[JsonSerializable(typeof(SessionSummaryEntry))]
[JsonSerializable(typeof(SessionMessage))]
[JsonSerializable(typeof(SDKSessionInfo))]
[JsonSerializable(typeof(ForkSessionResult))]
internal sealed partial class ClaudeAgentJsonContext : JsonSerializerContext;
