namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Outbound <c>interrupt</c> control request — asks the CLI to stop the
/// current turn (model invocation, tool execution). No additional fields.
/// Mirrors the Python SDK at <c>_internal/query.py:682–684</c>.
/// </summary>
/// <remarks>
/// Wire shape: <c>{ "subtype": "interrupt" }</c>. See
/// <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/interrupt_request.json</c>.
/// </remarks>
internal sealed record InterruptRequest : ControlRequestPayload;
