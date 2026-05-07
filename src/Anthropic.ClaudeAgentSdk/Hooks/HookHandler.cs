using System.Threading;
using System.Threading.Tasks;
using Anthropic.ClaudeAgentSdk.Hooks.Inputs;

namespace Anthropic.ClaudeAgentSdk.Hooks;

/// <summary>
/// Strongly-typed hook callback. Use this form when the registering site
/// knows the exact <typeparamref name="TInput"/> the matcher targets.
/// </summary>
public delegate ValueTask<HookJsonOutput> HookHandler<in TInput>(
    TInput input,
    string? toolUseId,
    HookContext context,
    CancellationToken cancellationToken
)
    where TInput : HookInput;

/// <summary>
/// Type-erased hook callback used for storage and dispatch when the
/// concrete <see cref="HookInput"/> variant is not statically known.
/// Wraps a <see cref="HookHandler{TInput}"/> by closing over the
/// strongly-typed delegate (acceptance criterion for master plan task 3.5).
/// </summary>
public delegate ValueTask<HookJsonOutput> HookHandler(
    HookInput input,
    string? toolUseId,
    HookContext context,
    CancellationToken cancellationToken
);
