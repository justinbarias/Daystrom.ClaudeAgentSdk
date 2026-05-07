using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Hooks.Outputs;

namespace Daystrom.ClaudeAgentSdk.Hooks;

/// <summary>
/// Wire shape of a hook handler's response. Mirrors
/// <c>claude_agent_sdk.types.HookJSONOutput</c>: a sync object with
/// control + decision fields, OR an async object that defers the response.
/// </summary>
/// <remarks>
/// <para>
/// Sync vs. async is distinguished by which fields are set. The Python
/// SDK uses an untagged union (no <c>type</c> discriminator) — the CLI
/// inspects whether <c>async</c> is present. The .NET SDK exposes both
/// shapes on a single record with optional fields and a small helper to
/// split into <c>SyncHookJsonOutput</c> vs. <c>AsyncHookJsonOutput</c>
/// callsite-side. Keeps the wire flat and source-gen-friendly.
/// </para>
/// <para>
/// <c>continue</c> and <c>async</c> are C# keywords; the wire field names
/// require explicit <see cref="JsonPropertyNameAttribute"/> to map.
/// </para>
/// </remarks>
public sealed record HookJsonOutput
{
    /// <summary>Set <c>true</c> to defer the hook response.</summary>
    [JsonPropertyName("async")]
    public bool? Async { get; init; }

    /// <summary>Optional async-defer timeout in milliseconds.</summary>
    [JsonPropertyName("asyncTimeout")]
    public int? AsyncTimeout { get; init; }

    /// <summary>Whether Claude should proceed after the hook.</summary>
    [JsonPropertyName("continue")]
    public bool? Continue { get; init; }

    /// <summary>Hide stdout from transcript display.</summary>
    [JsonPropertyName("suppressOutput")]
    public bool? SuppressOutput { get; init; }

    /// <summary>Message shown to the user when <see cref="Continue"/> is false.</summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }

    /// <summary>Decision flag — currently only <c>"block"</c> is meaningful.</summary>
    public string? Decision { get; init; }

    /// <summary>Warning message shown to the user.</summary>
    [JsonPropertyName("systemMessage")]
    public string? SystemMessage { get; init; }

    /// <summary>Feedback for the model about the decision.</summary>
    public string? Reason { get; init; }

    /// <summary>Event-specific extra output.</summary>
    [JsonPropertyName("hookSpecificOutput")]
    public HookSpecificOutput? HookSpecificOutput { get; init; }
}
