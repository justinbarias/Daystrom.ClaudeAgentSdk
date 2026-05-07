using System;
using System.Text.Json;
using Daystrom.ClaudeAgentSdk.Errors;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Errors;

public class ErrorsSmokeTests
{
    [Fact]
    public void ClaudeSdkException_IsBaseClass_AndNotSealed()
    {
        Assert.False(typeof(ClaudeSdkException).IsSealed);
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(ClaudeSdkException)));
    }

    [Fact]
    public void CliConnectionException_DerivesFromBase()
    {
        var ex = new CliConnectionException("boom");
        Assert.Equal("boom", ex.Message);
        Assert.IsAssignableFrom<ClaudeSdkException>(ex);
    }

    [Fact]
    public void CliNotFoundException_AppendsCliPath_WhenProvided()
    {
        var ex = new CliNotFoundException("Claude Code not found", "/nope/claude");
        Assert.Equal("Claude Code not found: /nope/claude", ex.Message);
        Assert.Equal("/nope/claude", ex.CliPath);
        Assert.IsAssignableFrom<CliConnectionException>(ex);
        Assert.True(typeof(CliNotFoundException).IsSealed);
    }

    [Fact]
    public void CliNotFoundException_OmitsPath_WhenNull()
    {
        var ex = new CliNotFoundException("not found", cliPath: null);
        Assert.Equal("not found", ex.Message);
        Assert.Null(ex.CliPath);
    }

    [Fact]
    public void ProcessException_FormatsExitCodeAndStderr()
    {
        var ex = new ProcessException("CLI crashed", exitCode: 137, stderr: "killed");
        Assert.Equal(137, ex.ExitCode);
        Assert.Equal("killed", ex.Stderr);
        Assert.Contains("(exit code: 137)", ex.Message);
        Assert.Contains("Error output: killed", ex.Message);
        Assert.True(typeof(ProcessException).IsSealed);
    }

    [Fact]
    public void ProcessException_OmitsExitCode_WhenNull()
    {
        var ex = new ProcessException("CLI crashed");
        Assert.Null(ex.ExitCode);
        Assert.Null(ex.Stderr);
        Assert.Equal("CLI crashed", ex.Message);
    }

    [Fact]
    public void CliJsonDecodeException_PreservesRawLineAndInner()
    {
        var raw = "{\"this is not\": \"valid";
        JsonException? inner = null;
        try
        {
            JsonDocument.Parse(raw);
        }
        catch (JsonException jx)
        {
            inner = jx;
        }

        Assert.NotNull(inner);

        var ex = new CliJsonDecodeException(raw, inner!);
        Assert.Equal(raw, ex.RawLine);
        Assert.Same(inner, ex.InnerException);
        Assert.Contains("Failed to decode JSON:", ex.Message);
        Assert.True(typeof(CliJsonDecodeException).IsSealed);
    }

    [Fact]
    public void CliJsonDecodeException_TruncatesLongLines_InMessage()
    {
        var raw = new string('a', 500);
        var ex = new CliJsonDecodeException(raw, new JsonException("nope"));
        Assert.Equal(raw, ex.RawLine);
        Assert.True(ex.Message.Length < raw.Length, "message should preview, not include full raw");
    }
}
