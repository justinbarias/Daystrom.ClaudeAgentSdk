using System;
using System.IO;

namespace Daystrom.ClaudeAgentSdk.Transport;

/// <summary>
/// Production <see cref="IFileSystem"/> implementation backed by
/// <see cref="System.IO.File"/> and <see cref="System.Environment"/>.
/// </summary>
public sealed class DefaultFileSystem : IFileSystem
{
    /// <summary>Shared singleton; the default file system has no state.</summary>
    public static DefaultFileSystem Instance { get; } = new();

    private DefaultFileSystem() { }

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    /// <inheritdoc />
    public string GetUserHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
