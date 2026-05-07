using System.Threading;

namespace Daystrom.ClaudeAgentSdk.Hooks;

/// <summary>
/// Context information passed to hook handlers. Mirrors
/// <c>claude_agent_sdk.types.HookContext</c>.
/// </summary>
/// <remarks>
/// The Python SDK reserves a <c>signal</c> field "for future abort signal
/// support". The .NET SDK exposes a real <see cref="System.Threading.CancellationToken"/>
/// instead — <see cref="HookHandler{TInput}"/> takes a
/// <c>CancellationToken</c> directly so the property here is purely for
/// future API stability.
/// </remarks>
public sealed record HookContext
{
    /// <summary>
    /// Reserved for future use. Cancellation in v1 is delivered via the
    /// <see cref="System.Threading.CancellationToken"/> parameter on the
    /// hook delegate.
    /// </summary>
    public CancellationToken? Signal { get; init; }
}
