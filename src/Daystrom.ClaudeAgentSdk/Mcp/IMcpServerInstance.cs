namespace Daystrom.ClaudeAgentSdk.Mcp;

/// <summary>
/// Marker interface for an in-process MCP server instance. The concrete
/// implementation lives in the <c>Daystrom.ClaudeAgentSdk.Mcp</c> package
/// (Phase 10) and wraps the <c>ModelContextProtocol</c> SDK; the core
/// SDK exposes only this opaque handle so consumers configure
/// <see cref="McpSdkServerConfig"/> without depending on the MCP package
/// transitively.
/// </summary>
public interface IMcpServerInstance
{
    /// <summary>Logical server name, surfaced to the CLI.</summary>
    string Name { get; }
}
