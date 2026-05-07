using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Sandbox;

/// <summary>
/// Spec §7 (line 239–245) requires the SandboxSettings XML doc to call
/// out the native-Windows no-op caveat. This test guards against that
/// caveat silently rotting.
/// </summary>
public class SandboxXmlDocCaveatTest
{
    [Fact]
    public void SandboxSettings_XmlDoc_MentionsNativeWindowsNoOp()
    {
        var assembly = typeof(Daystrom.ClaudeAgentSdk.Sandbox.SandboxSettings).Assembly;
        var docPath = Path.ChangeExtension(assembly.Location, ".xml");
        Assert.True(File.Exists(docPath), $"XML doc file not found at {docPath}");

        var doc = XDocument.Load(docPath);
        var member = doc.Descendants("member")
            .FirstOrDefault(m =>
                (string?)m.Attribute("name") == "T:Daystrom.ClaudeAgentSdk.Sandbox.SandboxSettings"
            );
        Assert.NotNull(member);

        var text = member!.Value;
        Assert.Contains("native Windows", text);
        Assert.True(
            text.Contains("no-op", System.StringComparison.OrdinalIgnoreCase),
            "SandboxSettings XML doc must explicitly call out that the setting is a no-op on native Windows."
        );
    }
}
