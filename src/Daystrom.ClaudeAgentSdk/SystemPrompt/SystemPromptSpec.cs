namespace Daystrom.ClaudeAgentSdk.SystemPrompt;

/// <summary>
/// Sealed-hierarchy tagged union describing the system prompt configuration
/// for a session. Mirrors Python's
/// <c>str | SystemPromptPreset | SystemPromptFile | None</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike most discriminated unions in this SDK, <see cref="SystemPromptSpec"/>
/// is <b>not</b> a JSON wire type. The CLI receives the system prompt via
/// dedicated flags (<c>--system-prompt</c>, <c>--append-system-prompt</c>,
/// <c>--system-prompt-file</c>), so the internal <c>CommandBuilder</c>
/// pattern-matches on the variant at argv-emission time rather than
/// serialising through <see cref="Json.ClaudeAgentJsonContext"/>.
/// </para>
/// <para>
/// Use <see cref="SystemPromptText"/> for an ad-hoc string, or
/// <see cref="SystemPromptPreset"/> to opt into Claude Code's bundled prompt
/// (optionally appending custom instructions), or <see cref="SystemPromptFile"/>
/// to point at a file on disk.
/// </para>
/// </remarks>
public abstract record SystemPromptSpec
{
    private protected SystemPromptSpec() { }

    /// <summary>
    /// Convenience factory: wrap a plain string in <see cref="SystemPromptText"/>.
    /// </summary>
    public static SystemPromptSpec Text(string value) => new SystemPromptText(value);
}
