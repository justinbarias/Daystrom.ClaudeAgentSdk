using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Daystrom.ClaudeAgentSdk.Sandbox;

/// <summary>
/// Network configuration for sandboxed bash commands. All wire fields are
/// camelCase. Mirrors <c>claude_agent_sdk.types.SandboxNetworkConfig</c>.
/// </summary>
/// <remarks>
/// Like <see cref="SandboxSettings"/>, every field here is a no-op on
/// native Windows because the CLI does not implement sandboxing on that
/// platform.
/// </remarks>
public sealed record SandboxNetworkConfig
{
    /// <summary>Domains the sandbox may reach.</summary>
    [JsonPropertyName("allowedDomains")]
    public IReadOnlyList<string>? AllowedDomains { get; init; }

    /// <summary>Domains explicitly blocked even if matched by an allow list.</summary>
    [JsonPropertyName("deniedDomains")]
    public IReadOnlyList<string>? DeniedDomains { get; init; }

    /// <summary>If true (managed settings), only managed allow lists apply.</summary>
    [JsonPropertyName("allowManagedDomainsOnly")]
    public bool? AllowManagedDomainsOnly { get; init; }

    /// <summary>Specific Unix sockets the sandbox may connect to.</summary>
    [JsonPropertyName("allowUnixSockets")]
    public IReadOnlyList<string>? AllowUnixSockets { get; init; }

    /// <summary>If true, all Unix sockets are accessible (less secure).</summary>
    [JsonPropertyName("allowAllUnixSockets")]
    public bool? AllowAllUnixSockets { get; init; }

    /// <summary>If true, sandboxed processes may bind to localhost ports (macOS only).</summary>
    [JsonPropertyName("allowLocalBinding")]
    public bool? AllowLocalBinding { get; init; }

    /// <summary>Mach service names to allow (macOS only; supports trailing wildcard).</summary>
    [JsonPropertyName("allowMachLookup")]
    public IReadOnlyList<string>? AllowMachLookup { get; init; }

    /// <summary>HTTP proxy port if bringing your own proxy.</summary>
    [JsonPropertyName("httpProxyPort")]
    public int? HttpProxyPort { get; init; }

    /// <summary>SOCKS5 proxy port if bringing your own proxy.</summary>
    [JsonPropertyName("socksProxyPort")]
    public int? SocksProxyPort { get; init; }
}
