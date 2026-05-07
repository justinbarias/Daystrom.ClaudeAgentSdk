using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daystrom.ClaudeAgentSdk.Internal;

/// <summary>
/// Tears a child <see cref="Process"/> down using the spec §9 ladder:
/// close stdin → wait 5s → <see cref="Process.Kill(bool)"/> with
/// <c>entireProcessTree:false</c> → wait 5s → <see cref="Process.Kill(bool)"/>
/// with <c>entireProcessTree:true</c>. Idempotent.
/// </summary>
/// <remarks>
/// <para>
/// On Unix .NET's <see cref="Process.Kill(bool)"/> sends <c>SIGKILL</c>
/// outright; the SDK does not currently issue a graceful <c>SIGTERM</c>
/// step (the closest analogue is the stdin-EOF wait at the top of the
/// ladder). Adding a SIGTERM rung means P/Invoking <c>kill(2)</c>; that
/// belongs to a future task if a child is found that holds open after
/// stdin closes but before SIGKILL.
/// </para>
/// <para>
/// On Windows, <see cref="Process.Kill(bool)"/> calls
/// <c>TerminateProcess</c> in both flavours; the second call's
/// <c>entireProcessTree:true</c> walks the job-object tree and kills
/// descendants the first call missed.
/// </para>
/// </remarks>
public sealed class ProcessGracefulShutdown
{
    /// <summary>Default per-step wait. Spec §9 fixes this at 5 seconds.</summary>
    public static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly TimeSpan _stepTimeout;
    private readonly ILogger _logger;
    private int _shutdownStarted;

    /// <summary>Creates a shutdown helper bound to <paramref name="process"/>.</summary>
    /// <param name="process">The child process to shut down.</param>
    /// <param name="stepTimeout">
    /// Wait per step. Defaults to <see cref="DefaultStepTimeout"/>; tests
    /// pass a smaller value to keep the suite fast.
    /// </param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger"/>.</param>
    public ProcessGracefulShutdown(
        Process process,
        TimeSpan? stepTimeout = null,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(process);
        _process = process;
        _stepTimeout = stepTimeout ?? DefaultStepTimeout;
        if (_stepTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stepTimeout),
                _stepTimeout,
                "Step timeout must be positive."
            );
        }
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>True once the shutdown ladder has been entered. Idempotent.</summary>
    public bool HasShutdown => Volatile.Read(ref _shutdownStarted) != 0;

    /// <summary>
    /// Walks the shutdown ladder. Subsequent calls return immediately
    /// without re-entering the sequence (idempotent).
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        // Step 1: close stdin to signal EOF. Some children exit on stdin
        // EOF without further prodding.
        TryCloseStdin();
        if (await WaitForExitAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Step 2: kill just this process.
        TryKill(entireProcessTree: false);
        if (await WaitForExitAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Step 3: escalate to the entire process tree.
        TryKill(entireProcessTree: true);
        try
        {
            // Best-effort final wait. We don't apply a timeout here — at
            // this point the kernel will terminate the tree imminently.
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsSwallowable(ex))
        {
            _logger.LogDebug(ex, "WaitForExitAsync after Kill(true) threw; continuing.");
        }
    }

    private async Task<bool> WaitForExitAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_stepTimeout);
        try
        {
            await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Step timed out, escalate.
            return _process.HasExited;
        }
    }

    private void TryCloseStdin()
    {
        try
        {
            if (_process.StartInfo.RedirectStandardInput)
            {
                _process.StandardInput.Close();
            }
        }
        catch (Exception ex) when (IsSwallowable(ex))
        {
            _logger.LogDebug(ex, "Closing child stdin threw; continuing.");
        }
    }

    private void TryKill(bool entireProcessTree)
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree);
            }
        }
        catch (Exception ex) when (IsSwallowable(ex))
        {
            _logger.LogDebug(
                ex,
                "Process.Kill(entireProcessTree: {Tree}) threw; continuing.",
                entireProcessTree
            );
        }
    }

    private static bool IsSwallowable(Exception ex) =>
        ex
            is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException
                or System.IO.IOException
                or ObjectDisposedException;
}
