using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daystrom.ClaudeAgentSdk.Sessions;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Sessions;

/// <summary>
/// Symmetric round-trip and wire-shape assertions for the Session DTOs.
/// Per the Phase 3 plan, no fake fixture JSON is synthesised — these
/// tests exercise STJ serialize → deserialize → equality and verify
/// snake-case wire field names. Fixture-driven round-trips against
/// captured CLI output land with Phase 9.
/// </summary>
public class SessionDtoRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void SessionKey_RoundTripsAndOmitsSubpathWhenNull()
    {
        var key = new SessionKey { ProjectKey = "p", SessionId = "s" };
        var json = JsonSerializer.Serialize(key, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("p", doc.RootElement.GetProperty("project_key").GetString());
        Assert.Equal("s", doc.RootElement.GetProperty("session_id").GetString());

        var roundTripped = JsonSerializer.Deserialize<SessionKey>(json, Options);
        Assert.Equal(key, roundTripped);
    }

    [Fact]
    public void SessionStoreEntry_PreservesOpaqueFieldsViaExtensionData()
    {
        const string json = """
            {
              "type": "user",
              "uuid": "u-1",
              "timestamp": "2026-05-07T00:00:00Z",
              "extra_field": {"nested": 42},
              "tag": "abc"
            }
            """;

        var entry = JsonSerializer.Deserialize<SessionStoreEntry>(json, Options);
        Assert.NotNull(entry);
        Assert.Equal("user", entry!.Type);
        Assert.Equal("u-1", entry.Uuid);
        Assert.Equal("2026-05-07T00:00:00Z", entry.Timestamp);
        Assert.NotNull(entry.AdditionalProperties);
        Assert.True(entry.AdditionalProperties!.ContainsKey("extra_field"));
        Assert.Equal("abc", entry.AdditionalProperties["tag"].GetString());

        // Round-trip preserves opaque fields.
        var serialised = JsonSerializer.Serialize(entry, Options);
        using var doc = JsonDocument.Parse(serialised);
        Assert.Equal(
            42,
            doc.RootElement.GetProperty("extra_field").GetProperty("nested").GetInt32()
        );
    }

    [Fact]
    public void SessionStoreListEntry_RoundTrips()
    {
        var entry = new SessionStoreListEntry { SessionId = "s-1", Mtime = 1_700_000_000_000L };
        var json = JsonSerializer.Serialize(entry, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("s-1", doc.RootElement.GetProperty("session_id").GetString());
        Assert.Equal(1_700_000_000_000L, doc.RootElement.GetProperty("mtime").GetInt64());

        Assert.Equal(entry, JsonSerializer.Deserialize<SessionStoreListEntry>(json, Options));
    }

    [Fact]
    public void SessionListSubkeysKey_HasNoSubpathField()
    {
        var key = new SessionListSubkeysKey { ProjectKey = "p", SessionId = "s" };
        var json = JsonSerializer.Serialize(key, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("p", doc.RootElement.GetProperty("project_key").GetString());
        Assert.Equal("s", doc.RootElement.GetProperty("session_id").GetString());
        Assert.False(doc.RootElement.TryGetProperty("subpath", out _));
    }

    [Fact]
    public void SessionSummaryEntry_RoundTripsWithOpaqueData()
    {
        var data = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement("hello"),
            ["count"] = JsonSerializer.SerializeToElement(7),
        };
        var entry = new SessionSummaryEntry
        {
            SessionId = "s-1",
            Mtime = 1_700_000_000_000L,
            Data = data,
        };

        var json = JsonSerializer.Serialize(entry, Options);
        var roundTripped = JsonSerializer.Deserialize<SessionSummaryEntry>(json, Options);
        Assert.NotNull(roundTripped);
        Assert.Equal("s-1", roundTripped!.SessionId);
        Assert.Equal(1_700_000_000_000L, roundTripped.Mtime);
        Assert.Equal("hello", roundTripped.Data["title"].GetString());
        Assert.Equal(7, roundTripped.Data["count"].GetInt32());
    }

    [Fact]
    public void SDKSessionInfo_OmitsNullableFieldsWhenUnset()
    {
        var info = new SDKSessionInfo
        {
            SessionId = "s-1",
            Summary = "first prompt",
            LastModified = 1_700_000_000_000L,
        };
        var json = JsonSerializer.Serialize(info, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("s-1", doc.RootElement.GetProperty("session_id").GetString());
        Assert.Equal("first prompt", doc.RootElement.GetProperty("summary").GetString());
        // Nullable fields serialise as null by default; that's fine for now —
        // the JsonContext (Task 3.8) sets WhenWritingNull globally.
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("file_size").ValueKind);

        var roundTripped = JsonSerializer.Deserialize<SDKSessionInfo>(json, Options);
        Assert.Equal(info, roundTripped);
    }

    [Fact]
    public void SDKSessionInfo_RoundTripsAllFields()
    {
        var info = new SDKSessionInfo
        {
            SessionId = "s",
            Summary = "sum",
            LastModified = 1L,
            FileSize = 2L,
            CustomTitle = "ct",
            FirstPrompt = "fp",
            GitBranch = "main",
            Cwd = "/work",
            Tag = "t",
            CreatedAt = 3L,
        };
        var json = JsonSerializer.Serialize(info, Options);
        Assert.Equal(info, JsonSerializer.Deserialize<SDKSessionInfo>(json, Options));
    }

    [Fact]
    public void SessionMessage_HoldsRawApiMessageAsJsonElement()
    {
        var inner = JsonSerializer.SerializeToElement(
            new { role = "user", content = "hi" },
            Options
        );
        var msg = new SessionMessage
        {
            Type = "user",
            Uuid = "u-1",
            SessionId = "s-1",
            Message = inner,
        };
        var json = JsonSerializer.Serialize(msg, Options);
        var roundTripped = JsonSerializer.Deserialize<SessionMessage>(json, Options);
        Assert.NotNull(roundTripped);
        Assert.Equal("user", roundTripped!.Type);
        Assert.Equal("u-1", roundTripped.Uuid);
        Assert.Equal("user", roundTripped.Message.GetProperty("role").GetString());
        Assert.Null(roundTripped.ParentToolUseId);
    }

    [Fact]
    public void ForkSessionResult_RoundTripsSingleField()
    {
        var result = new ForkSessionResult { SessionId = "forked-uuid" };
        var json = JsonSerializer.Serialize(result, Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("forked-uuid", doc.RootElement.GetProperty("session_id").GetString());
        Assert.Equal(result, JsonSerializer.Deserialize<ForkSessionResult>(json, Options));
    }

    [Fact(
        Skip = "Phase 9 fixture capture: replace synthetic JSON with a captured CLI transcript line."
    )]
    public void SessionStoreEntry_AgainstCapturedTranscriptLine()
    {
        // Placeholder: Phase 9 will capture a real CLI transcript JSONL line
        // and assert pass-through round-trip equality. Marked Skip so the
        // requirement isn't lost.
    }
}
