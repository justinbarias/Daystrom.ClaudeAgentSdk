using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Daystrom.ClaudeAgentSdk;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Messages.Content;
using Daystrom.ClaudeAgentSdk.Transport;

namespace StreamingMode;

// Phase 6 demo: drives ClaudeAgentClient through the streaming control
// protocol against a scripted in-process transport. Live-CLI streaming
// ships in Phase 14; this sample exists so CI compiles the streaming
// surface and a developer can `dotnet run --project samples/StreamingMode`
// to smoke-test the public Create(options, transport) factory without
// installing the Node CLI or supplying credentials.
internal static class Program
{
    private static async Task<int> Main()
    {
        var transport = new ScriptedTransport();
        var options = new ClaudeAgentOptions();

        await using var client = ClaudeAgentClient.Create(options, transport);
        await client.ConnectAsync();
        await client.SendUserMessageAsync("Say hello.");
        await client.EndInputAsync();

        await foreach (var message in client.ReceiveMessagesAsync())
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (var block in assistant.Message.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Console.WriteLine(text.Text);
                        }
                    }
                    break;
                case ResultMessage result:
                    Console.WriteLine(
                        $"-- done ({result.NumTurns} turn(s), {result.DurationMs} ms)"
                    );
                    break;
            }
        }

        return 0;
    }

    // Minimal in-process transport that scripts the bare-bones streaming
    // exchange the demo needs: it answers the SDK's `initialize` control
    // request, queues a single assistant turn plus a result, and ignores
    // the user message envelope. Real applications would either let
    // ClaudeAgentClient construct a SubprocessCliTransport (the default)
    // or implement ITransport against their own backend.
    private sealed class ScriptedTransport : ITransport
    {
        private readonly Channel<string> _inbound = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
        private bool _connected;
        private bool _closed;

        public bool IsReady => _connected && !_closed;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _connected = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(string data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryReadInitializeRequestId(data, out var requestId))
            {
                Enqueue(InitializeResponse(requestId));
                Enqueue(AssistantTurn("Hello from the scripted transport."));
                Enqueue(ResultTurn());
            }
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<JsonElement> ReadMessagesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            var reader = _inbound.Reader;
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var line))
                {
                    using var doc = JsonDocument.Parse(line);
                    yield return doc.RootElement.Clone();
                }
            }
        }

        public Task EndInputAsync(CancellationToken cancellationToken = default)
        {
            _closed = true;
            _inbound.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _closed = true;
            _inbound.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        private void Enqueue(string line)
        {
            if (!_inbound.Writer.TryWrite(line))
            {
                throw new InvalidOperationException(
                    "ScriptedTransport inbound channel rejected a write."
                );
            }
        }

        private static bool TryReadInitializeRequestId(string line, out string requestId)
        {
            requestId = string.Empty;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (
                root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.GetString() != "control_request"
                || !root.TryGetProperty("request", out var request)
                || !request.TryGetProperty("subtype", out var subtype)
                || subtype.GetString() != "initialize"
                || !root.TryGetProperty("request_id", out var id)
            )
            {
                return false;
            }
            requestId = id.GetString() ?? string.Empty;
            return requestId.Length > 0;
        }

        private static string InitializeResponse(string requestId) =>
            "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\""
            + requestId
            + "\",\"response\":{}}}";

        private static string AssistantTurn(string text) =>
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\""
            + text
            + "\"}],\"model\":\"claude-opus-4-7\"}}";

        private static string ResultTurn() =>
            """
                {"type":"result","subtype":"success","duration_ms":12,"duration_api_ms":3,"is_error":false,"num_turns":1,"session_id":"streaming-mode-demo"}
                """;
    }
}
