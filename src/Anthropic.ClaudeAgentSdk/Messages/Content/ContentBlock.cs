using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Discriminated union of every content-block kind that can appear inside a
/// <c>UserMessage</c> or <c>AssistantMessage</c>. The wire discriminator is
/// the <c>type</c> JSON field. Mirrors
/// <c>claude_agent_sdk.types.ContentBlock</c>.
/// </summary>
/// <remarks>
/// Discriminator strings are sourced from the Python SDK's
/// <c>_internal/message_parser.py</c>; see
/// <c>tests/Anthropic.ClaudeAgentSdk.Tests/Fixtures/discriminators.md</c>
/// for the full table. Note that <see cref="ServerToolResultBlock"/>'s wire
/// string is <c>advisor_tool_result</c>, not <c>server_tool_result</c> — the
/// CLI emits the advisor-flavored name and the type name reflects the
/// broader category.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ThinkingBlock), typeDiscriminator: "thinking")]
[JsonDerivedType(typeof(ToolUseBlock), typeDiscriminator: "tool_use")]
[JsonDerivedType(typeof(ToolResultBlock), typeDiscriminator: "tool_result")]
[JsonDerivedType(typeof(ServerToolUseBlock), typeDiscriminator: "server_tool_use")]
[JsonDerivedType(typeof(ServerToolResultBlock), typeDiscriminator: "advisor_tool_result")]
public abstract record ContentBlock;
