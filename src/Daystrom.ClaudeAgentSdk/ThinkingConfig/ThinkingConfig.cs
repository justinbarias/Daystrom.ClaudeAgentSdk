using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.ThinkingConfig;

/// <summary>
/// Discriminated union controlling Claude's extended-thinking behavior.
/// Wire discriminator: <c>type</c>. Mirrors
/// <c>claude_agent_sdk.types.ThinkingConfig</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ThinkingConfigAdaptive), typeDiscriminator: "adaptive")]
[JsonDerivedType(typeof(ThinkingConfigEnabled), typeDiscriminator: "enabled")]
[JsonDerivedType(typeof(ThinkingConfigDisabled), typeDiscriminator: "disabled")]
public abstract record ThinkingConfig;
