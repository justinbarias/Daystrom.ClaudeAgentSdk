using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Control.Requests;

namespace Daystrom.ClaudeAgentSdk.Control;

/// <summary>
/// Discriminated union of every control-request payload kind exchanged
/// between the SDK and the CLI. Discriminator: the wire <c>subtype</c>
/// field. Mirrors the Python SDK's <c>SDKControlRequest</c> union and the
/// <c>request_data["subtype"]</c> dispatch in
/// <c>_internal/query.py:339–423</c>.
/// </summary>
/// <remarks>
/// <para>
/// Outbound (SDK → CLI) variants the SDK constructs are:
/// <see cref="InitializeRequest"/>, <see cref="InterruptRequest"/>,
/// <see cref="SetPermissionModeRequest"/>, <see cref="McpStatusRequest"/>,
/// <see cref="GetContextUsageRequest"/>. (Phase 6 ships the wire types
/// for these and the streaming client's outbound calls.)
/// </para>
/// <para>
/// Inbound (CLI → SDK) variants we receive and dispatch are:
/// <see cref="CanUseToolRequest"/>, <see cref="HookCallbackRequest"/>,
/// <see cref="McpMessageRequest"/>. Concrete handler bodies for these
/// land in Phases 7 (hooks), 8 (permissions), and 10 (in-process MCP)
/// respectively; Phase 6 only registers the envelope shapes so the
/// dispatcher compiles.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "subtype")]
[JsonDerivedType(typeof(InitializeRequest), typeDiscriminator: "initialize")]
[JsonDerivedType(typeof(InterruptRequest), typeDiscriminator: "interrupt")]
[JsonDerivedType(typeof(SetPermissionModeRequest), typeDiscriminator: "set_permission_mode")]
[JsonDerivedType(typeof(McpStatusRequest), typeDiscriminator: "mcp_status")]
[JsonDerivedType(typeof(GetContextUsageRequest), typeDiscriminator: "get_context_usage")]
[JsonDerivedType(typeof(CanUseToolRequest), typeDiscriminator: "can_use_tool")]
[JsonDerivedType(typeof(HookCallbackRequest), typeDiscriminator: "hook_callback")]
[JsonDerivedType(typeof(McpMessageRequest), typeDiscriminator: "mcp_message")]
internal abstract record ControlRequestPayload;
