using System.Threading;
using System.Threading.Tasks;
using Anthropic.ClaudeAgentSdk.Hooks.Inputs;

namespace Anthropic.ClaudeAgentSdk.Hooks;

/// <summary>
/// Class-based hook handler. DI-friendly alternative to
/// <see cref="HookHandler{TInput}"/>. Implementations are registered via
/// <c>AddClaudeAgentHook&lt;T&gt;()</c> in the DI package.
/// </summary>
public interface IHookHandler<in TInput>
    where TInput : HookInput
{
    /// <summary>Invokes the hook with the given input.</summary>
    ValueTask<HookJsonOutput> HandleAsync(
        TInput input,
        string? toolUseId,
        HookContext context,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Non-generic class-based hook handler. Used by the DI/control-protocol
/// layers when the concrete input type is dispatched at runtime.
/// </summary>
public interface IHookHandler
{
    /// <summary>Invokes the hook.</summary>
    ValueTask<HookJsonOutput> HandleAsync(
        HookInput input,
        string? toolUseId,
        HookContext context,
        CancellationToken cancellationToken
    );
}
