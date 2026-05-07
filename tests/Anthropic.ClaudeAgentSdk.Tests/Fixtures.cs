using System;
using System.IO;
using System.Reflection;

namespace Anthropic.ClaudeAgentSdk.Tests;

/// <summary>
/// Loads embedded fixture JSON by logical path
/// (e.g. <c>"content/text.json"</c>). The csproj packs every file under
/// <c>Fixtures/</c> as an embedded resource via the <c>EmbeddedResource</c>
/// glob, so tests stay deterministic regardless of working directory.
/// </summary>
internal static class Fixtures
{
    private static readonly Assembly Assembly = typeof(Fixtures).Assembly;
    private const string ResourcePrefix = "Anthropic.ClaudeAgentSdk.Tests.Fixtures.";

    /// <summary>
    /// Reads the named fixture as a UTF-8 string. The <paramref name="path"/>
    /// is forward-slash separated and rooted at <c>Fixtures/</c>.
    /// </summary>
    public static string Read(string path)
    {
        // MSBuild's EmbeddedResource transforms folder separators into '.',
        // so "content/text.json" becomes "content.text.json" in the resource
        // name. The csproj-relative path arrives slash-separated; normalise.
        var resourceName = ResourcePrefix + path.Replace('/', '.').Replace('\\', '.');
        using var stream =
            Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded fixture '{resourceName}' not found. "
                    + "Available: "
                    + string.Join(", ", Assembly.GetManifestResourceNames())
            );
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
