using System;
using System.Text.Json;
using Daystrom.ClaudeAgentSdk.Json;
using Daystrom.ClaudeAgentSdk.Messages;
using Daystrom.ClaudeAgentSdk.Messages.Content;
using Daystrom.ClaudeAgentSdk.Sessions;

namespace AotSmoke;

/// <summary>
/// Manual AOT-publish smoke harness for <c>ClaudeAgentJsonContext</c>.
/// Exercises one serialise + deserialise round-trip per representative
/// wire shape so that <c>dotnet publish -p:PublishAot=true</c> surfaces
/// any IL2*/IL3* warnings against the source-gen context.
/// </summary>
/// <remarks>
/// The phase-checkpoint command is documented in <c>RELEASING.md</c>; this
/// program isn't a test and isn't run automatically — it just has to
/// publish cleanly. Phase 15.4 wires the actual CI gate.
/// </remarks>
internal static class Program
{
    private static int Main()
    {
        var ctx = ClaudeAgentJsonContext.Default;

        // Content block — round-trip a TextBlock through the polymorphic
        // base; this exercises the [JsonPolymorphic] codegen path.
        var text = new TextBlock("hello");
        var textJson = JsonSerializer.Serialize<ContentBlock>(text, ctx.ContentBlock);
        var textBack = JsonSerializer.Deserialize<ContentBlock>(textJson, ctx.ContentBlock);
        if (textBack is not TextBlock tb || tb.Text != "hello")
        {
            Console.Error.WriteLine("ContentBlock round-trip failed.");
            return 1;
        }

        // Session DTO — flat record with snake_case + extension data.
        var key = new SessionKey { ProjectKey = "p", SessionId = "s" };
        var keyJson = JsonSerializer.Serialize(key, ctx.SessionKey);
        var keyBack = JsonSerializer.Deserialize<SessionKey>(keyJson, ctx.SessionKey);
        if (keyBack is null || keyBack.ProjectKey != "p" || keyBack.SessionId != "s")
        {
            Console.Error.WriteLine("SessionKey round-trip failed.");
            return 1;
        }

        // Top-level Message — exercises the second polymorphic union.
        var msg = new SystemMessage { Subtype = "info" };
        var msgJson = JsonSerializer.Serialize<Message>(msg, ctx.Message);
        var msgBack = JsonSerializer.Deserialize<Message>(msgJson, ctx.Message);
        if (msgBack is not SystemMessage sm || sm.Subtype != "info")
        {
            Console.Error.WriteLine("Message round-trip failed.");
            return 1;
        }

        Console.WriteLine("aot-smoke: OK");
        return 0;
    }
}
