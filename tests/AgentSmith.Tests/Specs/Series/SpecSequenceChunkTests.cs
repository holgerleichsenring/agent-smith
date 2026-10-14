using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Series;

/// <summary>2026-10-13-8762u: one attempt splices a chunk — at most series.specs_per_attempt specs, fewer when the
/// measured blocks do not fit the wall clock LEFT — and defers the rest to the next attempt through g's marks.</summary>
public sealed class SpecSequenceChunkTests
{
    private const string Chunk = "beyond this attempt's chunk of";
    private readonly SettableClock _clock = new() { Now = DateTimeOffset.Parse("2026-10-14T10:00:00Z") };

    [Fact]
    public async Task SpecSequence_TwentyFourSpecs_SplicesEightAndMarksSixteenNextAttempt()
    {
        var pipeline = Pipeline(24);

        var result = await Sequence(pipeline);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().Equal(Ids(24).Take(8));
        var progress = Progress(pipeline);
        progress.Specs.Skip(8).Should().HaveCount(16).And.OnlyContain(p => p.NextAttempt && p.State == SpecRunState.NotStarted
            && p.Note == $"{Chunk} 8 spec(s) (series.specs_per_attempt and the wall clock left)");
        progress.Specs.Take(8).Should().OnlyContain(p => !p.NextAttempt);
        result.Message.Should().Contain($"the next attempt starts at {Ids(24)[8]}");
    }

    [Fact]
    public async Task SpecSequence_StepBudget_SizedFromTheChunk()
    {
        var pipeline = Pipeline(24);

        await Sequence(pipeline);

        pipeline.Get<StepBudget>(ContextKeys.StepBudget).Should().Be(StepBudget.ForSpecs(8));
    }

    [Fact]
    public async Task SpecSequence_ResumedSeries_SplicesTheNextChunkFromTheTail()
    {
        var pipeline = Pipeline(24, executed: 8);

        var result = await Sequence(pipeline);

        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().Equal(Ids(24).Skip(8).Take(8));
        Progress(pipeline).Specs.Take(8).Should().OnlyContain(p => p.State == SpecRunState.Done);
        Progress(pipeline).Specs.Skip(16).Should().OnlyContain(p => p.NextAttempt);
    }

    [Fact]
    public async Task SpecSequence_MeasuredBlocks_SplicesOnlyWhatFitsTheWallClock()
    {
        var pipeline = Pipeline(24);
        pipeline.Set(ContextKeys.RunStartedAt, _clock.Now);

        var result = await Sequence(pipeline, new SeriesConfig { SecondsPerSpec = 100 }, wallSeconds: 500);

        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().HaveCount(4, "five blocks fit, one is the tail's");
    }

    [Fact]
    public async Task SpecSequence_ElapsedWallClock_ShrinksTheChunk()
    {
        var pipeline = Pipeline(24);
        pipeline.Set(ContextKeys.RunStartedAt, _clock.Now.AddSeconds(-250));

        var result = await Sequence(pipeline, new SeriesConfig { SecondsPerSpec = 100 }, wallSeconds: 500);

        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().HaveCount(1, "250 s are left: two blocks, one is the tail's");
        Progress(pipeline).Specs[1].Note.Should().Be($"{Chunk} 1 spec(s) (series.specs_per_attempt and the wall clock left)");
    }

    [Fact]
    public async Task SpecSequence_NoBlockFitsTheTimeLeft_FailsNamingTheFields()
    {
        var pipeline = Pipeline(3);
        pipeline.Set(ContextKeys.RunStartedAt, _clock.Now.AddSeconds(-350));

        var result = await Sequence(pipeline, new SeriesConfig { SecondsPerSpec = 100 }, wallSeconds: 500);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("series.seconds_per_spec is 100").And.Contain("150 s are left")
            .And.Contain("orchestrator.max_run_wall_time_seconds (500)");
        pipeline.Has(ContextKeys.SpecSequenceProgress).Should().BeFalse();
    }

    [Fact]
    public async Task SpecSequence_InvalidSeriesConfig_FailsNamingTheField()
    {
        var result = await Sequence(Pipeline(3), new SeriesConfig { SpecsPerAttempt = 30 });

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().StartWith("The series settings are invalid: series.specs_per_attempt is 30");
    }

    [Fact]
    public async Task SpecSequence_StopAfterTheChunkEnd_DefersWithTheChunkReasonThenTheStopReason()
    {
        var pipeline = Pipeline(6, stop: new SpecSequenceStop(Ids(6)[4], "review share spent", Park: false));

        var result = await Sequence(pipeline, new SeriesConfig { SpecsPerAttempt = 2 });

        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().Equal(Ids(6).Take(2));
        Progress(pipeline).Specs.Select(p => (p.NextAttempt, p.Note)).Should().Equal(
            (false, null), (false, null),
            (true, $"{Chunk} 2 spec(s) (series.specs_per_attempt and the wall clock left)"),
            (true, $"{Chunk} 2 spec(s) (series.specs_per_attempt and the wall clock left)"),
            (true, "review share spent"), (true, "review share spent"));
    }

    [Fact]
    public async Task SpecSequence_ParkStopBeyondTheChunkEnd_DeferredNotParked()
    {
        var pipeline = Pipeline(4, stop: new SpecSequenceStop(Ids(4)[3], "the review's objection stands", Park: true));

        var result = await Sequence(pipeline, new SeriesConfig { SpecsPerAttempt = 2 });

        result.InsertNext!.Select(c => c.Name).Should().NotContain(CommandNames.SpecHandback);
        Progress(pipeline).Specs.Should().NotContain(p => p.State == SpecRunState.HandedBack);
        Progress(pipeline).Specs.Skip(2).Should().OnlyContain(p => p.NextAttempt && p.Note!.StartsWith(Chunk));
    }

    [Fact]
    public async Task SpecSequence_ReviewStopBeforeTheChunkEnd_SplicesOnlyTheSpecsBeforeTheStop()
    {
        var pipeline = Pipeline(24, stop: new SpecSequenceStop(Ids(24)[3], "review share spent", Park: false));

        var result = await Sequence(pipeline);

        result.InsertNext!.Select(c => c.SpecId).Distinct().Should().Equal(Ids(24).Take(3));
        Progress(pipeline).Specs.Skip(3).Should().OnlyContain(p => p.NextAttempt && p.Note == "review share spent");
        Progress(pipeline).StoppedAtChunk.Should().BeFalse("nothing is built yet");
        result.Message.Should().Contain($"stopping before {Ids(24)[3]}: review share spent");
    }

    private static IReadOnlyList<string> Ids(int count) =>
        [.. Enumerable.Range(0, count).Select(i => $"2026-10-14-5678{(char)('a' + i)}")];

    private static PipelineContext Pipeline(int count, int executed = 0, SpecSequenceStop? stop = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, new SpecSet(
            "azdo-1",
            [.. Ids(count).Select(id => new DerivedSpec(new SpecDraft(id, $"Goal of {id}", $"spec: {id}", []), id, string.Empty, []))],
            SpecAccounting.Empty, [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)], SpecSource.BranchArtifact,
            ExecutedSpecIds: [.. Ids(count).Take(executed)]));
        if (stop is not null) pipeline.Set(ContextKeys.SpecSequenceStop, stop);
        return pipeline;
    }

    private static SpecSequenceProgress Progress(PipelineContext pipeline) =>
        pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress);

    private Task<CommandResult> Sequence(PipelineContext pipeline, SeriesConfig? series = null, int wallSeconds = 1800) =>
        new SpecSequenceHandler(SeriesSettingsDoubles.Reader(series, wallSeconds), _clock, NullLogger<SpecSequenceHandler>.Instance)
            .ExecuteAsync(new SpecSequenceContext(pipeline), CancellationToken.None);
}
