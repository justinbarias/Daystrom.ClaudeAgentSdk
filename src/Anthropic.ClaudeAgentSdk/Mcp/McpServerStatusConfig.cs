using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Mcp;

/// <summary>
/// Output-only union of server configurations as returned in MCP status
/// responses. Differs from <see cref="McpServerConfig"/> by including
/// <see cref="McpClaudeAIProxyServerConfig"/> (a Claude.ai proxy variant
/// the CLI synthesises but consumers cannot configure directly) and an
/// SDK variant without the in-process instance handle.
/// </summary>
/// <remarks>
/// STJ's polymorphism does not support sharing a derived type across two
/// polymorphic bases, so the stdio/sse/http variants here are
/// status-specific records that mirror their <see cref="McpServerConfig"/>
/// counterparts field-for-field. Keeps source-gen happy.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(McpStdioServerStatusConfig), typeDiscriminator: "stdio")]
[JsonDerivedType(typeof(McpSseServerStatusConfig), typeDiscriminator: "sse")]
[JsonDerivedType(typeof(McpHttpServerStatusConfig), typeDiscriminator: "http")]
[JsonDerivedType(typeof(McpSdkServerConfigStatus), typeDiscriminator: "sdk")]
[JsonDerivedType(typeof(McpClaudeAIProxyServerConfig), typeDiscriminator: "claudeai-proxy")]
public abstract record McpServerStatusConfig;
