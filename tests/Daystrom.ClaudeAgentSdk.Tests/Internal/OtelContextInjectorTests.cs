using System.Collections.Generic;
using System.Diagnostics;
using Daystrom.ClaudeAgentSdk.Internal;
using Xunit;

namespace Daystrom.ClaudeAgentSdk.Tests.Internal;

public class OtelContextInjectorTests
{
    [Fact]
    public void Inject_NoActiveActivity_LeavesEnvUntouched()
    {
        // Sanity: ambient Activity.Current must be null in a fresh test.
        Assert.Null(Activity.Current);

        var processEnv = new Dictionary<string, string>
        {
            ["TRACEPARENT"] = "inherited-tp",
            ["TRACESTATE"] = "inherited-ts",
            ["OTHER"] = "stays",
        };
        var userEnv = new Dictionary<string, string>();

        OtelContextInjector.Inject(processEnv, userEnv);

        Assert.Equal("inherited-tp", processEnv["TRACEPARENT"]);
        Assert.Equal("inherited-ts", processEnv["TRACESTATE"]);
        Assert.Equal("stays", processEnv["OTHER"]);
    }

    [Fact]
    public void Inject_WithActiveActivity_WritesFreshContextAndScrubsStaleEnv()
    {
        using var listener = NewListener();
        using var source = new ActivitySource("Daystrom.ClaudeAgentSdk.Tests.Otel.Inject");
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("test")!;
        Assert.NotNull(activity);

        var processEnv = new Dictionary<string, string>
        {
            ["TRACEPARENT"] = "stale-from-ci",
            ["TRACESTATE"] = "stale-from-k8s",
            ["OTHER"] = "stays",
        };
        var userEnv = new Dictionary<string, string>();

        OtelContextInjector.Inject(processEnv, userEnv);

        Assert.True(processEnv.ContainsKey("TRACEPARENT"));
        Assert.NotEqual("stale-from-ci", processEnv["TRACEPARENT"]);
        Assert.Contains(activity.TraceId.ToString(), processEnv["TRACEPARENT"]);
        Assert.Equal("stays", processEnv["OTHER"]);
    }

    [Fact]
    public void Inject_UserEnvWins_DoesNotOverwriteOrScrub()
    {
        using var listener = NewListener();
        using var source = new ActivitySource("Daystrom.ClaudeAgentSdk.Tests.Otel.UserWins");
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("test")!;
        Assert.NotNull(activity);

        var processEnv = new Dictionary<string, string>
        {
            ["TRACEPARENT"] = "user-supplied-tp",
            ["TRACESTATE"] = "user-supplied-ts",
        };
        var userEnv = new Dictionary<string, string>
        {
            ["TRACEPARENT"] = "user-supplied-tp",
            ["TRACESTATE"] = "user-supplied-ts",
        };

        OtelContextInjector.Inject(processEnv, userEnv);

        Assert.Equal("user-supplied-tp", processEnv["TRACEPARENT"]);
        Assert.Equal("user-supplied-ts", processEnv["TRACESTATE"]);
    }

    [Fact]
    public void Inject_PartialUserOverride_OnlyKeepsTheOverriddenKey()
    {
        using var listener = NewListener();
        using var source = new ActivitySource("Daystrom.ClaudeAgentSdk.Tests.Otel.Partial");
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("test")!;
        Assert.NotNull(activity);

        var processEnv = new Dictionary<string, string>
        {
            ["TRACEPARENT"] = "user-supplied-tp",
            ["TRACESTATE"] = "stale-ts",
        };
        var userEnv = new Dictionary<string, string> { ["TRACEPARENT"] = "user-supplied-tp" };

        OtelContextInjector.Inject(processEnv, userEnv);

        // User's TRACEPARENT preserved; TRACESTATE was inherited (not in
        // userEnv) so it gets scrubbed and re-written if the propagator
        // emitted one — at minimum it must not be the stale value.
        Assert.Equal("user-supplied-tp", processEnv["TRACEPARENT"]);
        Assert.NotEqual("stale-ts", processEnv.GetValueOrDefault("TRACESTATE", string.Empty));
    }

    private static ActivityListener NewListener() =>
        new()
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { },
        };
}
