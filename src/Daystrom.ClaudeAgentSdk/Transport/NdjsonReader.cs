using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Daystrom.ClaudeAgentSdk.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daystrom.ClaudeAgentSdk.Transport;

/// <summary>
/// Streams NDJSON (newline-delimited JSON) from an arbitrary
/// <see cref="Stream"/> as a sequence of <see cref="JsonElement"/> values,
/// matching the Python SDK's speculative-buffer reader for parity with
/// the bundled CLI's <c>stream-json</c> output.
/// </summary>
/// <remarks>
/// <para>
/// The CLI emits one JSON object per line, but the underlying transport
/// (anyio's <c>TextReceiveStream</c>) does not guarantee that one
/// awaitable read corresponds to one JSON document — a single read can
/// concatenate or fragment lines. This reader buffers partial input until
/// <see cref="JsonDocument.Parse(string,JsonDocumentOptions)"/> succeeds,
/// matching <c>_read_messages_impl</c> in
/// <c>subprocess_cli.py</c> exactly.
/// </para>
/// <para>
/// Throws <see cref="CliJsonDecodeException"/> only when the in-flight
/// buffer exceeds <see cref="MaxBufferSize"/>; routine parse failures are
/// transparent (the buffer keeps accumulating until the next line
/// completes a valid document).
/// </para>
/// </remarks>
public sealed class NdjsonReader
{
    /// <summary>Default maximum bytes buffered before the reader gives up.</summary>
    public const int DefaultMaxBufferSize = 1024 * 1024;

    private readonly Stream _stream;
    private readonly ILogger _logger;

    /// <summary>Creates a reader over <paramref name="stream"/>.</summary>
    /// <param name="stream">Input stream; the reader does not dispose it.</param>
    /// <param name="maxBufferSize">
    /// Maximum bytes the in-flight buffer may grow before the reader
    /// throws. Null applies <see cref="DefaultMaxBufferSize"/>.
    /// </param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger"/>.</param>
    public NdjsonReader(Stream stream, int? maxBufferSize = null, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        MaxBufferSize = maxBufferSize ?? DefaultMaxBufferSize;
        if (MaxBufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBufferSize),
                MaxBufferSize,
                "MaxBufferSize must be a positive integer."
            );
        }
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Maximum bytes the in-flight buffer may grow before throwing.</summary>
    public int MaxBufferSize { get; }

    /// <summary>
    /// Reads the stream to EOF, yielding each successfully parsed JSON
    /// document as a cloned <see cref="JsonElement"/> the caller owns.
    /// </summary>
    public async IAsyncEnumerable<JsonElement> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var buffer = new StringBuilder();
        using var reader = new StreamReader(
            _stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true
        );

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? rawLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (rawLine is null)
            {
                yield break;
            }

            // ReadLine already strips a trailing newline; the secondary
            // split mirrors Python's tolerance for embedded \n that arrive
            // when the underlying transport batches multiple lines into
            // one read. CR remnants from CRLF are rare on stream-json but
            // cheap to strip.
            foreach (var part in rawLine.Split('\n'))
            {
                var trimmed = part.Trim('\r').Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (buffer.Length == 0 && trimmed[0] != '{')
                {
                    // Pre-amble noise from the CLI (warnings, log lines).
                    // Skip silently; the integration suite catches drift.
                    _logger.LogDebug(
                        "Skipping non-JSON line from CLI stdout: {Preview}",
                        Preview(trimmed)
                    );
                    continue;
                }

                buffer.Append(trimmed);

                if (buffer.Length > MaxBufferSize)
                {
                    var partial = buffer.ToString();
                    throw new CliJsonDecodeException(
                        partial,
                        new JsonException(
                            "NDJSON buffer exceeded maximum size of "
                                + $"{MaxBufferSize} bytes; raise "
                                + "ClaudeAgentOptions.MaxBufferSize or check "
                                + "the CLI for a runaway message."
                        )
                    );
                }

                JsonElement? cloned = null;
                try
                {
                    using var doc = JsonDocument.Parse(buffer.ToString());
                    cloned = doc.RootElement.Clone();
                    buffer.Clear();
                }
                catch (JsonException)
                {
                    // Keep accumulating; the next line probably completes
                    // the document.
                    continue;
                }

                if (cloned.HasValue)
                {
                    yield return cloned.Value;
                }
            }
        }
    }

    private static string Preview(string raw) => raw.Length > 200 ? raw.Substring(0, 200) : raw;
}
