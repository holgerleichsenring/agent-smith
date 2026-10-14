using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Passes;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Progress;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Passes.MasterPassFixtures;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: the recorder opens a pass only inside a spec step, names why it opened in the stated
/// order, keeps the run-wide numbering across a park, and its frame stamps what the pass publishes.
/// </summary>
public sealed class MasterPassRecorderTests
{
    private static readonly MasterPassState Empty = new(new ProgressLedger([]), [], null, null);

    [Fact]
    public async Task PassRecorder_AfterPark_ContinuesTheOrdinalAndOpensAfterAnswer()
    {
        var (events, runContext) = (new CapturingEvents(), new AsyncLocalRunContextAccessor());
        using var step = runContext.BeginStepScope(3, Spec);
        var recorder = Recorder(events, runContext);
        var pipeline = SpecPipeline();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "it builds" }); // asked inside a repair
        recorder.Open(pipeline, 0)!.Dispose();
        var asked = recorder.Open(pipeline, 1, MasterPassReasons.RedWithOpenSteps)!;
        await recorder.EndAsync(pipeline, asked, MasterPassReasons.Question, null, Empty);

        var serializer = new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance);
        var resumed = new PipelineContext();
        serializer.Restore(serializer.Serialize(pipeline), resumed);
        var after = recorder.Open(resumed, 0)!;

        (after.Ordinal, after.OpenedBecause, after.PassInStep).Should().Be((3, MasterPassReasons.AfterAnswer, 0));
        recorder.Open(resumed, 1)!.OpenedBecause.Should().Be(MasterPassReasons.Repair, "the answer is spent on one pass");
    }

    [Fact]
    public void PassRecorder_RepairStep_OpensBecauseRepair()
    {
        var pipeline = SpecPipeline();
        pipeline.Set(ContextKeys.SpecRepairAttempted, true);
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "it builds" });

        MasterPassRecorder.Opener(pipeline).Should().Be(MasterPassReasons.Repair);
    }

    [Fact]
    public void PassRecorder_ReviewFixPass_OpensBecauseReviewFix()
    {
        var pipeline = SpecPipeline();
        pipeline.Set(ContextKeys.StructureFindings, new List<string> { "too long" });
        SpecReviewFixPass.Prepare(pipeline);

        MasterPassRecorder.Opener(pipeline).Should().Be(MasterPassReasons.ReviewFix);
    }

    [Fact]
    public void PassRecorder_StructureCorrection_OpensBecauseStructureCorrection()
    {
        var pipeline = SpecPipeline();
        pipeline.Set(ContextKeys.StructureGateAttempted, true);
        pipeline.Set(ContextKeys.StructureFindings, new List<string> { "Handler.cs is 140 lines" });
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "it builds" });

        MasterPassRecorder.Opener(pipeline).Should().Be(MasterPassReasons.StructureCorrection);
        MasterPassRecorder.Opener(SpecPipeline()).Should().Be(MasterPassReasons.First);
    }

    [Fact]
    public async Task PassRecorder_ScanMaster_PublishesNothing()
    {
        var (events, runContext) = (new CapturingEvents(), new AsyncLocalRunContextAccessor());
        using var step = runContext.BeginStepScope(2); // a scan step belongs to no spec
        var recorder = Recorder(events, runContext);
        var pipeline = SpecPipeline();

        var pass = recorder.Open(pipeline, 0);
        await recorder.EndAsync(pipeline, pass, MasterPassReasons.NothingLeft, null, Empty);

        pass.Should().BeNull();
        events.All.Should().BeEmpty();
        pipeline.Has(ContextKeys.MasterPassOrdinal).Should().BeFalse();
    }

    [Fact]
    public async Task Publisher_InsidePass_StampsSpecAndPass()
    {
        var (events, runContext) = (new CapturingEvents(), new AsyncLocalRunContextAccessor());
        var publisher = new StepAttributingEventPublisher(events, runContext);
        using var step = runContext.BeginStepScope(4, Spec);
        var recorder = Recorder(events, runContext);
        var pipeline = SpecPipeline();
        pipeline.Set(ContextKeys.MasterPassOrdinal, 6);

        using (recorder.Open(pipeline, 0))
            await Task.Run(() => publisher.PublishAsync(Transitions())); // a child flow inherits the frame
        await publisher.PublishAsync(Transitions());
        await publisher.PublishAsync(Transitions() with { OriginSpecId = "own", OriginMasterPass = 1 });

        events.All.Select(e => (e.OriginStepIndex, e.OriginSpecId, e.OriginMasterPass)).Should().Equal(
            (4, Spec, 7), (4, Spec, (int?)null), (4, "own", 1));
    }

    private static LedgerTransitionsRecordedEvent Transitions() => new(RunId, "[]", DateTimeOffset.UnixEpoch);
}
