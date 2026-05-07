using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Daystrom.ClaudeAgentSdk.Permissions;

/// <summary>
/// User-supplied callback invoked by the CLI before a tool runs. Returns
/// either a <see cref="PermissionResultAllow"/> (optionally with a
/// rewritten <c>updated_input</c>) or a <see cref="PermissionResultDeny"/>.
/// Mirrors <c>claude_agent_sdk.types.CanUseTool</c>.
/// </summary>
public delegate ValueTask<PermissionResult> CanUseToolDelegate(
    string toolName,
    JsonElement input,
    ToolPermissionContext context,
    CancellationToken cancellationToken
);
