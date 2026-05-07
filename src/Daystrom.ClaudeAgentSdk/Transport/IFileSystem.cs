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

    /// <summary>Reads the entire contents of <paramref name="path"/> as UTF-8 text.</summary>
    /// <remarks>
    /// Used by <c>CommandBuilder</c> when merging a sandbox payload into a
    /// settings file referenced by path. Implementations may throw the
    /// usual <see cref="System.IO.IOException"/> family on failure.
    /// </remarks>
    string ReadAllText(string path);
}
