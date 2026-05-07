using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anthropic.ClaudeAgentSdk.Messages;

/// <summary>
/// System-event message. Wire discriminator: <c>system</c>. Carries a
/// second-level <see cref="Subtype"/> that selects between the typed
/// task-lifecycle subclasses (<see cref="TaskStartedMessage"/>,
/// <see cref="TaskProgressMessage"/>, <see cref="TaskNotificationMessage"/>,
/// <see cref="MirrorErrorMessage"/>) and an open-ended catch-all.
/// </summary>
/// <remarks>
/// STJ's polymorphism is single-level, so this record (rather than its
/// subtypes) is what STJ produces when deserialising any
/// <c>type: "system"</c> wire message. The <c>MessageParser</c> in Phase 5
/// inspects <see cref="Subtype"/> and synthesises the matching subclass
/// when one is recognised.
/// </remarks>
public record SystemMessage : Messages.Message
{
    /// <summary>Subtype discriminator for the second-level dispatch.</summary>
    public required string Subtype { get; init; }

    /// <summary>Raw payload — the original wire object captured verbatim.</summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}
