using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Sessions;

/// <summary>
/// Controls when the SDK forwards transcript-mirror frames to a configured
/// <c>ISessionStore</c>. Mirrors <c>claude_agent_sdk.types.SessionStoreFlushMode</c>.
/// </summary>
public enum SessionStoreFlushMode
{
    /// <summary>
    /// Buffer transcript-mirror frames and flush at end-of-turn or on a
    /// 500-entry / 1 MiB overflow. Default; minimises per-frame overhead.
    /// </summary>
    [JsonStringEnumMemberName("batched")]
    Batched,

    /// <summary>
    /// Forward each frame to the store as soon as the SDK observes it.
    /// Use when downstream consumers (live-tailing UIs, cross-process
    /// resume, crash-durability flows) need near-real-time visibility.
    /// </summary>
    [JsonStringEnumMemberName("eager")]
    Eager,
}
