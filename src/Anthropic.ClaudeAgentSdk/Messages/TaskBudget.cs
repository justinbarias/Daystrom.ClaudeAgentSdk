namespace Anthropic.ClaudeAgentSdk.Messages;

/// <summary>
/// API-side task budget in tokens. When set, the model is made aware of
/// its remaining token budget so it can pace tool use and wrap up before
/// the limit. Sent as <c>output_config.task_budget</c> with the
/// <c>task-budgets-2026-03-13</c> beta header.
/// </summary>
public sealed record TaskBudget
{
    /// <summary>Total budget in tokens.</summary>
    public required int Total { get; init; }
}
