using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Control.Requests;

/// <summary>
/// Outbound <c>initialize</c> control request — the very first frame the
/// SDK writes to a streaming session, declaring registered hooks, agents,
/// and skills, and toggling dynamic system-prompt sections. Mirrors the
/// Python SDK at <c>_internal/query.py:165–215</c>.
/// </summary>
/// <remarks>
/// <para>
/// Wire shape (Phase 6, no hooks/agents):
/// <c>{ "subtype": "initialize", "hooks": null, "agents": null, "skills": null }</c>.
/// See <c>tests/Daystrom.ClaudeAgentSdk.Tests/Fixtures/control/initialize_request.json</c>.
/// </para>
/// <para>
/// Field-naming asymmetry: <see cref="ExcludeDynamicSections"/> serialises
/// as camelCase <c>excludeDynamicSections</c> and <see cref="Skills"/>
/// stays snake-case <c>skills</c> — see Python <c>query.py:202–207</c>.
/// </para>
/// <para>
/// Phase 6 always sends <see cref="Hooks"/> and <see cref="Agents"/> as
/// null; full hook configuration lands in Phase 7, agents wiring later.
/// </para>
/// </remarks>
internal sealed record InitializeRequest : ControlRequestPayload
{
    /// <summary>
    /// Hooks configuration keyed by hook event. The CLI expects an array
    /// of opaque matcher objects (callback ids, matcher pattern, optional
    /// timeout). Phase 6 always sends null here; Phase 7 populates this
    /// from the registered <c>HookHandler</c> table.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<JsonElement>>? Hooks { get; init; }

    /// <summary>
    /// Agents declaration block (forward-compat placeholder). Phase 6
    /// always sends null; concrete agents wiring lands in a later phase.
    /// </summary>
    public JsonElement? Agents { get; init; }

    /// <summary>
    /// When non-null, the CLI will exclude dynamic system-prompt sections
    /// from initialization. Wire name is camelCase
    /// <c>excludeDynamicSections</c> (NOT snake-case) — see Python
    /// <c>_internal/client.py:165</c>.
    /// </summary>
    [JsonPropertyName("excludeDynamicSections")]
    public bool? ExcludeDynamicSections { get; init; }

    /// <summary>
    /// Explicit skills allowlist; null means "no filter, use defaults".
    /// Only sent when caller passed an explicit list (mirrors Python's
    /// <c>isinstance(self._skills, list)</c> check).
    /// </summary>
    public IReadOnlyList<string>? Skills { get; init; }
}
