using AgentSmith.Contracts.Runs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Replay;

/// <summary>
/// p0427: a run that already happened, replayed against the current code with no provider
/// call. Measured 2026-08-14..16: 45 runs, 33h52m of wall clock, 3 green — and every defect
/// found was deterministic and local. This is the instrument those runs did not have.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ReplayedRunTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"guarded the empty payload","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""";

    /// <summary>
    /// The whole loop: a run records itself, the recording is read back, and the recording
    /// alone drives the same pipeline to the same writes — no scripted answers, no model.
    /// </summary>
    [Fact]
    public async Task ARunRecordedByTheHarness_ReplaysToTheSameToolCalls_WithoutAModel()
    {
        var recorded = await RecordAsync();

        recorded.Answers.Should().HaveCountGreaterThan(3,
            "every provider call is its own entry — analyzer, derivation and each tool round");

        await using var replay = ReplayedRun.Of(recorded);
        var result = await replay.Runner.RunAsync("fix-bug");

        result.Should().NotBeNull("the replayed run must reach a terminal result");
        replay.Client.Served.Should().Be(recorded.Answers.Count,
            "the replayed code must ask for exactly the calls the recording contains");
        replay.WrittenPaths.Should().Contain(p => p.EndsWith("src/Patch.cs", StringComparison.Ordinal),
            "the recorded tool call must run for real against the sandbox on replay");
    }

    /// <summary>
    /// The committed scenario: the analyzer answered <c>file_count: null</c>, which ended
    /// run 27 at step 12 on 2026-08-16. The recording carries that exact shape; the current
    /// code replays it to a terminal result instead of an exception.
    /// </summary>
    [Fact]
    public async Task TheRecordedNullFileCount_ReplaysThroughTheRealComposition_WithoutEndingTheRun()
    {
        var trace = await RecordedRunFixtures.LoadAsync(RecordedRunFixtures.NullFileCountRun);

        trace.Answers.Should().Contain(a => a.Contains("\"file_count\": null", StringComparison.Ordinal),
            "the committed scenario must still carry the shape that ended run 27");

        await using var replay = ReplayedRun.Of(trace);
        var result = await replay.Runner.RunAsync("fix-bug");

        result.Should().NotBeNull(
            "before p0426 this recording ended the run at the parse boundary; it must now finish");
        replay.Client.Remaining.Should().Be(0,
            "a replay that leaves recorded answers unclaimed means the code stopped asking");
    }

    /// <summary>
    /// Recordings get shared. Masking happens once, at write time — this proves the
    /// committed artefact is clean, so nobody is asked to trust that it was.
    /// </summary>
    [Fact]
    public async Task ACommittedScenario_CarriesNoCredential()
    {
        var trace = await RecordedRunFixtures.LoadAsync(RecordedRunFixtures.NullFileCountRun);
        var recorded = string.Join("\n", trace.Entries.Select(e => e.Content));

        foreach (var secret in new[] { "harness-pat", "glpat-", "sk-ant-", "_authToken=", "Bearer " })
            recorded.Should().NotContain(secret, $"a shared recording must not carry '{secret}'");
    }

    /// <summary>
    /// The recordings worth replaying are of runs that DIED, so a replay must survive
    /// running out of record: the exhaustion surfaces as a failed model call, which the
    /// framework already knows how to finalize.
    /// </summary>
    [Fact]
    public async Task TheReplayOfAnIncompleteRecording_StillFinalizesTheRun()
    {
        var complete = await RecordedRunFixtures.LoadAsync(RecordedRunFixtures.NullFileCountRun);
        var cut = RecordedTrace.Of(complete.Entries.Take(2));

        await using var replay = ReplayedRun.Of(cut);
        var result = await replay.Runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeFalse(
            "a run whose recording stops mid-flight cannot be reported as a success");
        replay.Client.Remaining.Should().Be(0, "the replay served everything the record had");
    }

    // The recording half: the scripted harness run whose trace the committed scenario was
    // exported from. Keeping it here means the fixture can be regenerated from the same
    // shapes it was made of.
    private static async Task<RecordedTrace> RecordAsync()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), TracedHarness.RecordTheConversation);
        harness.ChatClient
            .EnqueueText(RecordedRunFixtures.AnalyzerMapWithNullFileCount)
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// guard"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("fix-bug");

        var trace = await TracedHarness.ReadAsync(harness.Services, runner.LastRunId!);
        await RecordedRunFixtures.ExportIfRequestedAsync(trace);
        return trace;
    }
}
