using System.Text.Json;

namespace Daystrom.ClaudeAgentSdk.OutputFormat;

/// <summary>
/// Output-format variant requesting structured JSON output validated
/// against the supplied schema. Emitted as <c>--json-schema &lt;json&gt;</c>
/// on the CLI command line.
/// </summary>
/// <param name="Schema">
/// JSON Schema body. Must be valid JSON; the SDK does not validate the
/// schema itself — the CLI / model handles that. Stored as a
/// <see cref="JsonElement"/> to keep the type AOT-clean and avoid
/// reflection-based binding.
/// </param>
public sealed record OutputFormatJsonSchema(JsonElement Schema) : OutputFormatSpec;
