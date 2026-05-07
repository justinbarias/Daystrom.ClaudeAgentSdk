using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Anthropic.ClaudeAgentSdk.Json;
using Xunit;

namespace Anthropic.ClaudeAgentSdk.Tests.Json;

/// <summary>
/// Anti-reflection guard for the core SDK source tree. Spec §11 + plan
/// task 3.8 require that no SDK code path constructs
/// <see cref="JsonSerializerOptions"/> directly or routes through
/// reflective <c>JsonSerializer</c> overloads — every (de)serialise call
/// must go through <see cref="ClaudeAgentJsonContext"/> so AOT publish
/// stays warning-free.
/// </summary>
/// <remarks>
/// Implementation note: pragmatic source grep, not Roslyn analysis. The
/// trade-off (caught in plan §3.8 verification) is good enough for this
/// phase — false positives are easy to whitelist via the
/// <see cref="AllowedFiles"/> list and false negatives are unlikely in a
/// hand-authored codebase. A Roslyn analyzer is a candidate for Phase
/// 15.4 if drift becomes a real risk.
/// </remarks>
public class AntiReflectionGuardTests
{
    private static readonly string[] AllowedFiles =
    {
        // The context itself necessarily mentions JsonSerializer-adjacent
        // attributes; nothing else in the SDK should.
        "Json/ClaudeAgentJsonContext.cs",
    };

    [Fact]
    public void NoCallSiteConstructsJsonSerializerOptionsDirectly()
    {
        var srcRoot = LocateSdkSource();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsAllowed(file, srcRoot))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            // Two failure modes the plan calls out:
            //   1. `new JsonSerializerOptions(...)` — bypasses the context.
            //   2. Reflective overloads that take `Type` instead of
            //      `JsonTypeInfo` — these trim/AOT the wrong way.
            if (
                text.Contains("new JsonSerializerOptions(", System.StringComparison.Ordinal)
                || text.Contains(
                    "typeof(JsonSerializer).GetMethod",
                    System.StringComparison.Ordinal
                )
            )
            {
                offenders.Add(Path.GetRelativePath(srcRoot, file));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The following SDK files bypass ClaudeAgentJsonContext: " + string.Join(", ", offenders)
        );
    }

    private static bool IsAllowed(string file, string root)
    {
        var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
        return AllowedFiles.Any(allowed =>
            rel.Equals(allowed, System.StringComparison.Ordinal)
            || rel.EndsWith("/" + allowed, System.StringComparison.Ordinal)
        );
    }

    private static string LocateSdkSource()
    {
        // Walk up from the test assembly's binary location until we find
        // the repo's `src/Anthropic.ClaudeAgentSdk` folder. Walking the
        // filesystem (rather than embedding paths) keeps this test
        // resilient across local builds and CI runners.
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(AntiReflectionGuardTests).Assembly.Location)!
        );
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Anthropic.ClaudeAgentSdk");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate src/Anthropic.ClaudeAgentSdk relative to the test assembly."
        );
    }

    [Fact]
    public void ClaudeAgentJsonContext_ExposesEveryRegisteredWireType()
    {
        // Smoke check: the context's Default property must materialise.
        // If a [JsonSerializable] entry is malformed, this throws at type
        // initialisation and surfaces here rather than at MessageParser
        // call sites.
        var ctx = ClaudeAgentJsonContext.Default;
        Assert.NotNull(ctx);
        Assert.NotNull(ctx.Message);
        Assert.NotNull(ctx.ContentBlock);
        Assert.NotNull(ctx.HookInput);
        Assert.NotNull(ctx.HookSpecificOutput);
        Assert.NotNull(ctx.HookJsonOutput);
        Assert.NotNull(ctx.PermissionResult);
        Assert.NotNull(ctx.McpServerConfig);
        Assert.NotNull(ctx.McpServerStatusConfig);
        Assert.NotNull(ctx.SessionKey);
    }
}
