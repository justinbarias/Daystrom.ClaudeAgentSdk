using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Messages.Content;

/// <summary>
/// Name of a server-side tool the API executes on the model's behalf
/// (advisor, web_search, code_execution, etc.). Carried by
/// <c>ServerToolUseBlock</c>; branch on it to know which tool's
/// result to expect downstream. Mirrors
/// <c>claude_agent_sdk.types.ServerToolName</c>.
/// </summary>
public enum ServerToolName
{
    /// <summary>Anthropic-hosted advisor tool.</summary>
    [JsonStringEnumMemberName("advisor")]
    Advisor,

    /// <summary>Web search.</summary>
    [JsonStringEnumMemberName("web_search")]
    WebSearch,

    /// <summary>Web fetch.</summary>
    [JsonStringEnumMemberName("web_fetch")]
    WebFetch,

    /// <summary>Code execution.</summary>
    [JsonStringEnumMemberName("code_execution")]
    CodeExecution,

    /// <summary>Bash code execution.</summary>
    [JsonStringEnumMemberName("bash_code_execution")]
    BashCodeExecution,

    /// <summary>Text-editor code execution.</summary>
    [JsonStringEnumMemberName("text_editor_code_execution")]
    TextEditorCodeExecution,

    /// <summary>Tool search via regex.</summary>
    [JsonStringEnumMemberName("tool_search_tool_regex")]
    ToolSearchToolRegex,

    /// <summary>Tool search via BM25.</summary>
    [JsonStringEnumMemberName("tool_search_tool_bm25")]
    ToolSearchToolBm25,
}
