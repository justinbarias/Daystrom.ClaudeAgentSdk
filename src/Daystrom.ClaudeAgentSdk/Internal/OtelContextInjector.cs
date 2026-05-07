using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daystrom.ClaudeAgentSdk.Internal;

/// <summary>
/// Propagates the caller's active <see cref="Activity"/> into a child
/// process's environment so the CLI's spans parent under the caller's
/// distributed trace, mirroring the Python SDK's
/// <c>opentelemetry.propagate.inject</c> step in
/// <c>subprocess_cli.py</c>.
/// </summary>
/// <remarks>
/// <para>
/// Uses BCL <see cref="DistributedContextPropagator.Current"/> only — the
/// core package must not depend on the OpenTelemetry SDK (per CLAUDE.md
/// architectural rails). Best-effort: any exception is swallowed and
/// logged at debug level so a tracing misconfiguration never blocks
/// process spawn.
/// </para>
/// <para>
/// Scrubbing rules match Python's: when an active <see cref="Activity"/>
/// produces a fresh <c>traceparent</c>, stale inherited <c>TRACEPARENT</c>
/// / <c>TRACESTATE</c> values are removed before writing the new ones, so
/// an old <c>TRACESTATE</c> never pairs with a new <c>TRACEPARENT</c>. An
/// explicit value in <c>options.Env</c> always wins — the user's intent
/// takes precedence over both inherited env and the freshly-injected
/// context.
/// </para>
/// </remarks>
public static class OtelContextInjector
{
    /// <summary>
    /// Injects the active trace context into <paramref name="processEnv"/>,
    /// scrubbing stale W3C keys when a fresh context is available.
    /// </summary>
    /// <param name="processEnv">
    /// The mutable environment dictionary that will be passed to
    /// <see cref="System.Diagnostics.ProcessStartInfo"/>. Modified in
    /// place.
    /// </param>
    /// <param name="userEnv">
    /// The caller's explicit environment overrides
    /// (<c>ClaudeAgentOptions.Env</c>). Keys present here are never
    /// scrubbed or overwritten — the user's intent wins.
    /// </param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger"/>.</param>
    public static void Inject(
        IDictionary<string, string> processEnv,
        IReadOnlyDictionary<string, string> userEnv,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(processEnv);
        ArgumentNullException.ThrowIfNull(userEnv);
        logger ??= NullLogger.Instance;

        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        try
        {
            var propagator = DistributedContextPropagator.Current;
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);
            propagator.Inject(
                activity,
                carrier,
                static (carrierObj, fieldName, fieldValue) =>
                {
                    if (carrierObj is Dictionary<string, string> dict && fieldName is not null)
                    {
                        dict[fieldName] = fieldValue;
                    }
                }
            );

            // Gate on the traceparent key — a baggage-only or non-W3C
            // carrier should not scrub a valid inherited TRACEPARENT.
            if (!carrier.ContainsKey("traceparent"))
            {
                return;
            }

            foreach (var staleKey in s_staleKeys)
            {
                if (!userEnv.ContainsKey(staleKey))
                {
                    processEnv.Remove(staleKey);
                }
            }

            foreach (var (key, value) in carrier)
            {
                var upper = key.ToUpperInvariant();
                if (!userEnv.ContainsKey(upper))
                {
                    processEnv[upper] = value;
                }
            }
        }
        catch (Exception ex)
        {
            // Best-effort: tracing failures must never break connect().
            logger.LogDebug(ex, "OTEL trace context injection failed");
        }
    }

    private static readonly string[] s_staleKeys = ["TRACEPARENT", "TRACESTATE"];
}
