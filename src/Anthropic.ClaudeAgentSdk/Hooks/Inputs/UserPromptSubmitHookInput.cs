namespace Anthropic.ClaudeAgentSdk.Hooks.Inputs;

/// <summary>Hook fired when the user submits a prompt to the model.</summary>
public sealed record UserPromptSubmitHookInput : HookInput
{
    /// <summary>The submitted prompt text.</summary>
    public required string Prompt { get; init; }
}
