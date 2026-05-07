namespace Anthropic.ClaudeAgentSdk.Agents.Plugins;

/// <summary>
/// SDK plugin configuration. Currently only <see cref="Type"/> = <c>local</c>
/// is supported, which loads a plugin from a local directory at
/// <see cref="Path"/>. Mirrors <c>claude_agent_sdk.types.SdkPluginConfig</c>.
/// </summary>
public sealed record SdkPluginConfig
{
    /// <summary>Plugin loader type. Currently always <c>local</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Filesystem path to the plugin directory.</summary>
    public required string Path { get; init; }
}
