using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Runs;

/// <summary>
/// p0369: the RunMetrics fold is a pure function of the event stream — this
/// pins the time split, tool tallies, top files, CONTENT-AWARE redundancy
/// (same path + same hash = waste; same path + changed hash = legitimate),
/// cache health, and build/test counts from scripted events.
/// </summary>
public sealed class RunMetricsComputerTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
    private const string Run = "r1";

    private static LlmCallFinishedEvent Llm(
        long tokensIn, long durationMs, long throttleMs, long cachedIn = 0, long creationIn = 0) =>
        new(Run, "m", "coder", tokensIn, 10, 0.01m, durationMs, T,
            CachedTokensIn: cachedIn, CacheCreationTokensIn: creationIn, ThrottleWaitMs: throttleMs);

    private static SandboxResultEvent Sandbox(
        string command, long durationMs = 0, int exit = 0, string? summary = null, string? hash = null) =>
        new(Run, "repo", command, exit, durationMs, T, Summary: summary, ContentHash: hash);

    [Fact]
    public void TimeSplit_SumsActiveAndThrottleAndSandboxMs()
    {
        var view = RunMetrics.From(new RunEvent[]
        {
            Llm(tokensIn: 100, durationMs: 1000, throttleMs: 300),
            Llm(tokensIn: 100, durationMs: 2000, throttleMs: 700),
            Sandbox("ReadFile", durationMs: 50, summary: "a.cs", hash: "h1"),
            Sandbox("/bin/sh", durationMs: 4000, summary: "-c echo hi"),
        }).ToView();

        view.LlmActiveMs.Should().Be(3000);
        view.ThrottleWaitMs.Should().Be(1000);
        view.SandboxCommandMs.Should().Be(4050);
    }

    [Fact]
    public void ToolCounts_AndTopFiles_ByVerb()
    {
        var view = RunMetrics.From(new RunEvent[]
        {
            Sandbox("ReadFile", summary: "a.cs", hash: "h1"),
            Sandbox("ReadFile", summary: "a.cs", hash: "h2"),
            Sandbox("ReadFile", summary: "b.cs", hash: "h1"),
            Sandbox("WriteFile", summary: "a.cs", hash: "w1"),
            Sandbox("Grep", summary: "x in a.cs"),
            Sandbox("/bin/sh", summary: "-c echo hi"),
            Sandbox("ListFiles", summary: "dir"),      // neither read/write/run/grep
            Sandbox("DirectoryTree", summary: "dir"),
        }).ToView();

        view.Reads.Should().Be(3);
        view.Writes.Should().Be(1);
        view.Greps.Should().Be(1);
        view.RunCommands.Should().Be(1, "ListFiles/DirectoryTree are not shell runs");
        view.TopReadFiles.Should().ContainInOrder(
            new FileAccessView("a.cs", 2), new FileAccessView("b.cs", 1));
    }

    [Fact]
    public void RedundantReads_SameHashRepeated_Counted_ChangedContent_NotCounted()
    {
        var view = RunMetrics.From(new RunEvent[]
        {
            Sandbox("ReadFile", summary: "a.cs", hash: "h1"),
            Sandbox("ReadFile", summary: "a.cs", hash: "h1"),  // identical re-read -> redundant
            Sandbox("ReadFile", summary: "a.cs", hash: "h2"),  // content CHANGED -> legitimate
            Sandbox("ReadFile", summary: "b.cs", hash: "hb"),  // first read of b -> not redundant
        }).ToView();

        view.RedundantReads.Should().Be(1);
    }

    [Fact]
    public void RedundantWrites_IdenticalContent_Counted()
    {
        var view = RunMetrics.From(new RunEvent[]
        {
            Sandbox("WriteFile", summary: "a.cs", hash: "w1"),
            Sandbox("WriteFile", summary: "a.cs", hash: "w1"),  // rewrote identical content
            Sandbox("WriteFile", summary: "a.cs", hash: "w2"),
        }).ToView();

        view.RedundantWrites.Should().Be(1);
    }

    [Fact]
    public void CacheHealth_ColdCalls_Discards_AndReprocessedTokens()
    {
        var view = RunMetrics.From(new RunEvent[]
        {
            Llm(tokensIn: 1500, durationMs: 0, throttleMs: 0, cachedIn: 2000),  // warm
            Llm(tokensIn: 2000, durationMs: 0, throttleMs: 0),                  // cold + discard
            Llm(tokensIn: 3000, durationMs: 0, throttleMs: 0),                  // cold, no discard
            Llm(tokensIn: 500, durationMs: 0, throttleMs: 0),                   // small -> not cold
        }).ToView();

        view.ColdLlmCalls.Should().Be(2);
        view.CacheDiscards.Should().Be(1, "only the warm->cold transition is a discard");
        view.UncachedReprocessedTokens.Should().Be(5000);
    }

    [Fact]
    public void BuildTest_CountsInvocationsAndFailures()
    {
        var view = RunMetrics.From(new RunEvent[]
        {
            Sandbox("/bin/sh", exit: 0, summary: "-c dotnet build"),
            Sandbox("/bin/sh", exit: 1, summary: "-c dotnet test tests/X.csproj"),
            Sandbox("/bin/sh", exit: 0, summary: "-c echo hi"),  // not build/test
        }).ToView();

        view.BuildTestInvocations.Should().Be(2);
        view.BuildTestFailures.Should().Be(1);
        view.RunCommands.Should().Be(3);
    }

    [Fact]
    public void EmptyStream_YieldsZeroedView()
    {
        var view = RunMetrics.From(Array.Empty<RunEvent>()).ToView();

        view.Reads.Should().Be(0);
        view.TopReadFiles.Should().BeEmpty();
        view.LlmActiveMs.Should().Be(0);
    }
}
