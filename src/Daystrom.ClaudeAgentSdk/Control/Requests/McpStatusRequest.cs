namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Outbound <c>mcp_status</c> control request — asks the CLI for the
/// current connection status of every configured MCP server. The success
/// response body deserialises to <see cref="Mcp.McpStatusResponse"/>.
/// Mirrors the Python SDK at <c>_internal/query.py:674–676</c>.
/// </summary>
/// <remarks>
/// Wire shape: <c>{ "subtype": "mcp_status" }</c>. See
/// <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/mcp_status_request.json</c>.
/// </remarks>
internal sealed record McpStatusRequest : ControlRequestPayload;
