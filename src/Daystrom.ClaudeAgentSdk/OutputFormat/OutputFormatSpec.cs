namespace Daystrom.ClaudeAgentSdk.OutputFormat;

/// <summary>
/// Sealed-hierarchy tagged union describing structured-output configuration
/// for a session. Currently only <see cref="OutputFormatJsonSchema"/> is
/// defined; future variants will land here as the CLI gains them.
/// </summary>
/// <remarks>
/// Like <see cref="SystemPrompt.SystemPromptSpec"/>, this is an SDK-side
/// tagged variant rather than a stream-json wire type — the CLI receives
/// the schema via <c>--json-schema &lt;json&gt;</c>, and the internal
/// <c>CommandBuilder</c> pattern-matches on the variant at argv-emission
/// time.
/// </remarks>
public abstract record OutputFormatSpec
{
    private protected OutputFormatSpec() { }
}
