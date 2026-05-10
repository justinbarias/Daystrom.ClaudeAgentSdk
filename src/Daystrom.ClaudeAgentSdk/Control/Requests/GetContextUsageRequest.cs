namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Outbound <c>get_context_usage</c> control request — asks the CLI for
/// a breakdown of the active context window's token usage by category.
/// The success response body deserialises to
/// <see cref="Messages.ContextUsageResponse"/>. Mirrors the Python SDK at
/// <c>_internal/query.py:678–680</c>.
/// </summary>
/// <remarks>
/// Wire shape: <c>{ "subtype": "get_context_usage" }</c>. See
/// <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/get_context_usage_request.json</c>.
/// </remarks>
internal sealed record GetContextUsageRequest : ControlRequestPayload;
