using System.Text.Json;

namespace Daystrom.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Tool-call content block emitted by the assistant. <see cref="Input"/> is
/// surfaced as a raw <see cref="JsonElement"/> because each tool defines
/// its own input schema; consumers branch on <see cref="Name"/> and
/// deserialize <see cref="Input"/> against the appropriate shape. Wire
/// discriminator: <c>tool_use</c>.
/// </summary>
public sealed record ToolUseBlock(string Id, string Name, JsonElement Input) : ContentBlock;
