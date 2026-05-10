using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Inbound <c>mcp_message</c> control request — the CLI is forwarding a
/// JSON-RPC message bound for one of our in-process SDK MCP servers. The
/// handler dispatches <see cref="Message"/> through the named server's
/// MCP runtime and replies with the JSON-RPC response. Mirrors the
/// Python SDK at <c>_internal/query.py:405–420</c>.
/// </summary>
/// <remarks>
/// Wire shape:
/// <c>{ "subtype": "mcp_message", "server_name": "my-tools", "message": { "jsonrpc": "2.0", "method": "tools/call", ... } }</c>.
/// Concrete handler ships in Phase 10 (in-process MCP servers); Phase 6
/// only registers the envelope so the dispatcher compiles. See
/// <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/mcp_message_request.json</c>.
/// </remarks>
internal sealed record McpMessageRequest : ControlRequestPayload
{
    /// <summary>Name of the SDK MCP server to route this message to.</summary>
    [JsonPropertyName("server_name")]
    public required string ServerName { get; init; }

    /// <summary>Opaque JSON-RPC message body.</summary>
    public required JsonElement Message { get; init; }
}
