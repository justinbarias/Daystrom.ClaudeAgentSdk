using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk.Errors;
using Daystrom.ClaudeAgentSdk.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daystrom.ClaudeAgentSdk.Transport;

/// <summary>
/// Default <see cref="ITransport"/> implementation that spawns the bundled
/// Claude Code CLI as a child process and exchanges <c>stream-json</c>
/// NDJSON over its stdio. Mirrors
/// <c>claude_agent_sdk._internal.transport.subprocess_cli.SubprocessCLITransport</c>.
/// </summary>
/// <remarks>
/// <para>
/// This phase ships the one-shot capable transport — it can spawn the CLI
/// in either streaming or <c>--print</c> mode and read NDJSON to EOF. The
/// control-protocol initialise handshake and the streaming-write
/// machinery layer on top of this in Phase 6.
/// </para>
/// <para>
/// Stdin writes are serialised through a <see cref="SemaphoreSlim"/>
/// (mirrors Python's <c>_write_lock</c>). On dispose the transport runs
/// the spec §9 graceful shutdown ladder via
/// <see cref="ProcessGracefulShutdown"/>: close stdin → wait → kill → wait
/// → kill tree.
/// </para>
/// </remarks>
public sealed class SubprocessCliTransport : ITransport
{
    private const string EntryPointEnvKey = "CLAUDE_CODE_ENTRYPOINT";
    private const string EntryPointValue = "sdk-dotnet";
    private const string SdkVersionEnvKey = "CLAUDE_AGENT_SDK_VERSION";
    private const string ParentSentinelEnvKey = "CLAUDECODE";
    private const string FileCheckpointEnvKey = "CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING";

    private readonly ClaudeAgentOptions _options;
    private readonly CliBinaryResolver _resolver;
    private readonly TransportMode _mode;
    private readonly string? _oneShotPrompt;
    private readonly IReadOnlyList<string>? _argvOverride;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private Process? _process;
    private NdjsonReader? _reader;
    private Task? _stderrTask;
    private CancellationTokenSource? _stderrCts;
    private ProcessGracefulShutdown? _shutdown;
    private int _disposed;

    /// <summary>
    /// Creates a transport bound to <paramref name="options"/>. Does not
    /// spawn the process — call <see cref="ConnectAsync"/>.
    /// </summary>
    /// <param name="options">User-supplied session options.</param>
    /// <param name="oneShotPrompt">
    /// When non-null, the transport spawns the CLI in <c>--print</c>
    /// one-shot mode using this string as the prompt argument; stdin is
    /// closed immediately after the process starts. Pass <c>null</c> for
    /// the streaming mode used by the Phase 6 control protocol.
    /// </param>
    /// <param name="resolver">CLI binary resolver. Defaults to a fresh instance.</param>
    /// <param name="argvOverride">
    /// Test-only escape hatch: when non-null, this argv is used verbatim
    /// for spawning the child process and the resolver/<see cref="CommandBuilder"/>
    /// pipeline is skipped. Used by transport tests to spawn a fixture
    /// binary that responds to a known mode flag rather than the real CLI.
    /// </param>
    /// <param name="logger">Optional logger.</param>
    public SubprocessCliTransport(
        ClaudeAgentOptions options,
        string? oneShotPrompt = null,
        CliBinaryResolver? resolver = null,
        IReadOnlyList<string>? argvOverride = null,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _resolver = resolver ?? new CliBinaryResolver(logger: logger);
        _mode = oneShotPrompt is null ? TransportMode.Streaming : TransportMode.OneShot;
        _oneShotPrompt = oneShotPrompt;
        _argvOverride = argvOverride;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public bool IsReady => _process is { HasExited: false };

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
        {
            return;
        }

        IReadOnlyList<string> argv;
        if (_argvOverride is not null)
        {
            argv = _argvOverride;
        }
        else
        {
            var cliPath = _resolver.Resolve(_options);
            var builder = new CommandBuilder(cliPath, _options, logger: _logger);
            argv =
                _mode == TransportMode.OneShot
                    ? builder.BuildOneShot(_oneShotPrompt!)
                    : builder.BuildStreaming();
        }

        var psi = new ProcessStartInfo
        {
            FileName = argv[0],
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = _options.Stderr is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _options.Cwd ?? string.Empty,
        };
        for (var i = 1; i < argv.Count; i++)
        {
            psi.ArgumentList.Add(argv[i]);
        }

        ApplyEnvironment(psi);

        Process process;
        try
        {
            process =
                Process.Start(psi)
                ?? throw new CliConnectionException("Process.Start returned null.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new CliNotFoundException($"Claude Code not found at: {argv[0]}", argv[0]);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new CliConnectionException(
                $"Working directory does not exist: {_options.Cwd}",
                ex
            );
        }
        catch (Exception ex)
            when (ex is not CliConnectionException and not OperationCanceledException)
        {
            throw new CliConnectionException($"Failed to start Claude Code: {ex.Message}", ex);
        }

        _process = process;
        _shutdown = new ProcessGracefulShutdown(process, logger: _logger);
        _reader = new NdjsonReader(
            process.StandardOutput.BaseStream,
            maxBufferSize: _options.MaxBufferSize,
            logger: _logger
        );

        if (_options.Stderr is not null)
        {
            _stderrCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _stderrTask = Task.Run(
                () => DrainStderrAsync(process, _options.Stderr, _stderrCts.Token),
                _stderrCts.Token
            );
        }

        if (_mode == TransportMode.OneShot)
        {
            // One-shot mode: the prompt is on argv; close stdin immediately
            // so the CLI knows there's no streaming input.
            try
            {
                process.StandardInput.Close();
            }
            catch (Exception ex) when (IsSwallowable(ex))
            {
                _logger.LogDebug(ex, "Closing stdin in one-shot mode threw; continuing.");
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureConnected();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is null || _process.HasExited)
            {
                throw new CliConnectionException(
                    _process is null
                        ? "Transport is not connected."
                        : $"Cannot write to terminated process (exit code: {_process.ExitCode})."
                );
            }

            try
            {
                await _process
                    .StandardInput.WriteAsync(data.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
                when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                throw new CliConnectionException("Failed to write to process stdin.", ex);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<JsonElement> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        EnsureConnected();
        await foreach (var element in _reader!.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return element;
        }

        // After EOF we still want to surface non-zero exits so callers can
        // diagnose them. ProcessException is thrown only when the CLI
        // produced no usable output AND exited non-zero.
        if (_process is not null)
        {
            try
            {
                await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsSwallowable(ex))
            {
                _logger.LogDebug(ex, "WaitForExit after stdout EOF threw; continuing.");
            }

            if (_process.HasExited && _process.ExitCode != 0)
            {
                throw new ProcessException(
                    $"Claude Code CLI exited with non-zero status.",
                    _process.ExitCode
                );
            }
        }
    }

    /// <inheritdoc />
    public async Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is not null)
            {
                try
                {
                    _process.StandardInput.Close();
                }
                catch (Exception ex) when (IsSwallowable(ex))
                {
                    _logger.LogDebug(ex, "Closing stdin threw; continuing.");
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_stderrCts is not null)
        {
            try
            {
                _stderrCts.Cancel();
            }
            catch (Exception ex) when (IsSwallowable(ex))
            {
                _logger.LogDebug(ex, "Cancelling stderr CTS threw; continuing.");
            }
        }

        if (_shutdown is not null && _process is not null)
        {
            await _shutdown.ShutdownAsync().ConfigureAwait(false);
        }

        if (_stderrTask is not null)
        {
            try
            {
                await _stderrTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (IsSwallowable(ex) || ex is OperationCanceledException)
            {
                // Already shutting down — swallow.
            }
        }

        _stderrCts?.Dispose();
        _process?.Dispose();
        _writeLock.Dispose();
        _process = null;
        _reader = null;
        _stderrTask = null;
        _stderrCts = null;
        _shutdown = null;
    }

    private void EnsureConnected()
    {
        if (_process is null || _reader is null)
        {
            throw new CliConnectionException(
                "Transport is not connected. Call ConnectAsync first."
            );
        }
    }

    private void ApplyEnvironment(ProcessStartInfo psi)
    {
        // Filter inherited CLAUDECODE — issue #573 in the Python SDK: an
        // SDK-spawned subprocess shouldn't think it's running inside a
        // Claude Code parent.
        psi.Environment.Remove(ParentSentinelEnvKey);

        psi.Environment[EntryPointEnvKey] = EntryPointValue;
        foreach (var (key, value) in _options.Env)
        {
            psi.Environment[key] = value;
        }
        psi.Environment[SdkVersionEnvKey] = ClaudeAgent.SdkVersion;

        OtelContextInjector.Inject(psi.Environment!, _options.Env, _logger);

        if (_options.EnableFileCheckpointing)
        {
            psi.Environment[FileCheckpointEnvKey] = "true";
        }

        if (!string.IsNullOrEmpty(_options.Cwd))
        {
            psi.Environment["PWD"] = _options.Cwd;
        }
    }

    private static async Task DrainStderrAsync(
        Process process,
        Action<string> callback,
        CancellationToken cancellationToken
    )
    {
        try
        {
            string? line;
            while (
                (
                    line = await process
                        .StandardError.ReadLineAsync(cancellationToken)
                        .ConfigureAwait(false)
                )
                    is not null
            )
            {
                if (line.Length == 0)
                {
                    continue;
                }
                callback(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose.
        }
        catch (Exception)
        {
            // Mirror Python: swallow stderr-reader failures so they never
            // bubble out of dispose.
        }
    }

    private static bool IsSwallowable(Exception ex) =>
        ex
            is InvalidOperationException
                or Win32Exception
                or NotSupportedException
                or IOException
                or ObjectDisposedException;

    private enum TransportMode
    {
        Streaming,
        OneShot,
    }
}
