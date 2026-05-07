namespace Daystrom.ClaudeAgentSdk.Transport;

/// <summary>
/// Minimal filesystem abstraction used by <see cref="CliBinaryResolver"/>
/// so each branch of the lookup order can be exercised in unit tests
/// without touching a real disk. Production code uses the default
/// implementation, <see cref="DefaultFileSystem"/>.
/// </summary>
public interface IFileSystem
{
    /// <summary>Returns true when a regular file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Reads an environment variable. Returns null when unset.</summary>
    string? GetEnvironmentVariable(string name);

    /// <summary>Returns the current user's home directory (used for the fallback paths).</summary>
    string GetUserHome();
}
