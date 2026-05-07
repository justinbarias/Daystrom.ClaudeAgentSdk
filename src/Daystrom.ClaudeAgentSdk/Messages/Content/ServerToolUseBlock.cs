using System.Text.Json;

namespace Daystrom.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Server-side tool call block (advisor, web_search, web_fetch, etc.) —
/// tools the API executes server-side on the model's behalf. They appear
/// in the message stream alongside regular <see cref="ToolUseBlock"/>s but
/// the caller never needs to return a result. Branch on
/// <see cref="Name"/> to know which server tool was invoked. Wire
/// discriminator: <c>server_tool_use</c>.
/// </summary>
public sealed record ServerToolUseBlock(string Id, ServerToolName Name, JsonElement Input)
    : ContentBlock;
