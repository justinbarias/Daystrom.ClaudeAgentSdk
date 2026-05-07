namespace Anthropic.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Extended-thinking content block. <see cref="Thinking"/> may be omitted by
/// the API depending on <c>ThinkingDisplay</c>; <see cref="Signature"/> is
/// always present and lets the API verify the block round-trips unmodified.
/// Wire discriminator: <c>thinking</c>.
/// </summary>
public sealed record ThinkingBlock(string Thinking, string Signature) : ContentBlock;
