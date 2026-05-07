using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages;

/// <summary>
/// Discriminated union of every top-level NDJSON message kind the CLI can
/// emit. Discriminator: the wire <c>type</c> field. Mirrors
/// <c>claude_agent_sdk.types.Message</c>.
/// </summary>
/// <remarks>
/// <para>
/// Note that <see cref="SystemMessage"/> carries a second-level
/// <c>subtype</c> field. STJ's polymorphism is single-level, so direct
/// deserialization of an arbitrary system message yields a base
/// <see cref="SystemMessage"/>. The <c>MessageParser</c> introduced in
/// Phase 5 inspects <see cref="SystemMessage.Subtype"/> and synthesises
/// the appropriate concrete type (<see cref="TaskStartedMessage"/>,
/// <see cref="TaskProgressMessage"/>, <see cref="TaskNotificationMessage"/>,
/// <see cref="MirrorErrorMessage"/>) when present.
/// </para>
/// <para>
/// Forward compatibility: unknown wire <c>type</c> values are skipped by
/// the parser (they do not throw) — this matches the Python SDK's
/// <c>parse_message</c> behavior at <c>_internal/message_parser.py:281</c>.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserMessage), typeDiscriminator: "user")]
[JsonDerivedType(typeof(AssistantMessage), typeDiscriminator: "assistant")]
[JsonDerivedType(typeof(SystemMessage), typeDiscriminator: "system")]
[JsonDerivedType(typeof(ResultMessage), typeDiscriminator: "result")]
[JsonDerivedType(typeof(StreamEvent), typeDiscriminator: "stream_event")]
[JsonDerivedType(typeof(RateLimitEvent), typeDiscriminator: "rate_limit_event")]
public abstract record Message;
