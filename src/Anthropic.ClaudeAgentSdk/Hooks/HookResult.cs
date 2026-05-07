using System.Text.Json;

namespace Anthropic.ClaudeAgentSdk.Hooks;

/// <summary>
/// Result of a hook handler. Internal SDK type — not a wire-polymorphic
/// type. The control-protocol routing layer (Phase 7) flattens this into
/// the wire-shape <see cref="HookJsonOutput"/> before the response goes
/// to the CLI.
/// </summary>
public abstract record HookResult
{
    /// <summary>Allow execution to continue without modifications.</summary>
    public sealed record Continue : HookResult;

    /// <summary>
    /// Block the operation. <see cref="Reason"/> is surfaced to the model
    /// so it can adapt its plan.
    /// </summary>
    public sealed record Block(string Reason) : HookResult;

    /// <summary>
    /// Modify the operation by replacing its input with
    /// <see cref="Patch"/>. Used by hooks that rewrite tool inputs (e.g.
    /// to redact secrets before <c>Bash</c> runs).
    /// </summary>
    public sealed record Modify(JsonElement Patch) : HookResult;
}
