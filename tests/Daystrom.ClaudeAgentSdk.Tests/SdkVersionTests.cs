using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests;

public class SdkVersionTests
{
    [Fact]
    public void SdkVersion_IsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ClaudeAgent.SdkVersion));
    }

    [Fact]
    public void SdkVersion_MatchesVersionsProps()
    {
        var versionsProps = LocateVersionsProps();
        var doc = XDocument.Load(versionsProps);
        var declared = doc.Descendants("SdkVersion").Single().Value.Trim();

        // The SDK strips any "+<sha>" build-metadata suffix; compare the
        // SemVer core only.
        var declaredCore = declared.Split('+')[0];

        Assert.Equal(declaredCore, ClaudeAgent.SdkVersion);
    }

    private static string LocateVersionsProps()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "eng", "Versions.props");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "Could not locate eng/Versions.props from test bin output."
        );
    }
}
