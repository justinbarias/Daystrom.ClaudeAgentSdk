using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>
/// Discriminated union of MCP server configurations. Wire discriminator:
/// <c>type</c>. Mirrors <c>claude_agent_sdk.types.McpServerConfig</c>.
/// </summary>
/// <remarks>
/// On the wire, <c>type</c> is technically optional for stdio for backward
/// compatibility — the CLI infers stdio when no type is present. STJ's
/// polymorphism requires the discriminator to be present; the
/// <c>MessageParser</c> in Phase 5 normalises legacy payloads by injecting
/// <c>"type": "stdio"</c> when missing. Direct deserialisation of a
/// type-less payload through this base record will throw.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(McpStdioServerConfig), typeDiscriminator: "stdio")]
[JsonDerivedType(typeof(McpSseServerConfig), typeDiscriminator: "sse")]
[JsonDerivedType(typeof(McpHttpServerConfig), typeDiscriminator: "http")]
[JsonDerivedType(typeof(McpSdkServerConfig), typeDiscriminator: "sdk")]
public abstract record McpServerConfig;
