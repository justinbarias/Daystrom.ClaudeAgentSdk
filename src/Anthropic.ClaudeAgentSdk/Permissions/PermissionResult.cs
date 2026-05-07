using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Permissions;

/// <summary>
/// Result of a tool-permission check. Wire discriminator is the
/// <c>behavior</c> field — <b>not</b> <c>type</c> as the design spec
/// initially indicated. Mirrors the TypeScript SDK's
/// <c>PermissionResult</c> and the Python <c>PermissionResultAllow</c> /
/// <c>PermissionResultDeny</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "behavior")]
[JsonDerivedType(typeof(PermissionResultAllow), typeDiscriminator: "allow")]
[JsonDerivedType(typeof(PermissionResultDeny), typeDiscriminator: "deny")]
public abstract record PermissionResult;
