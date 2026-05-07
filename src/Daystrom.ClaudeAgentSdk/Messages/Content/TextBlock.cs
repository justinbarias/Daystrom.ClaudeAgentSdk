namespace Daystrom.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Plain-text content block. Wire discriminator: <c>text</c>.
/// </summary>
public sealed record TextBlock(string Text) : ContentBlock;
