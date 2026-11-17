using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-17-7c9bc: a design turn's proposal spend is what the run recorded, worker calls included; the
/// tracker it used to read leaves those out, so a worker-mode turn showed usd 0.
/// </summary>
public sealed class TurnSpendTests
{
    [Fact]
    public void Of_WorkerCallInTheLedger_SpendIsItsCostWhileTheTrackerSaysZero()
    {
        var pipeline = new PipelineContext();
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        tracker.TrackWorkerCall(new WorkerCallAccounting("claude-opus-5-5", 2, 900, 0, 0, 0.42m, 1));
        var ledger = new RecordedCallLedger();
        ledger.Add(new RecordedCall("claude-opus-5-5", "Plan", null, 2, 900, 0, 0, 0.42m, IsWorker: true));
        RecordedCallAttach.To(pipeline, ledger);

        var spend = TurnSpend.Of(pipeline);

        tracker.EstimateCostUsd().Should().Be(0m);
        spend!.Usd.Should().Be(0.42m);
    }

    [Fact]
    public void Of_AResumedTurn_AddsTheEarlierSegments()
    {
        var pipeline = new PipelineContext();
        PipelineCostTracker.GetOrCreate(pipeline);
        pipeline.Set(ContextKeys.PriorSpend, new PriorSpendSnapshot(0, 0, 0, 0, 0.25m, 0, 0, 0, 0, 0.75m, "claude-opus-5-5"));
        var ledger = new RecordedCallLedger();
        ledger.Add(new RecordedCall("claude-opus-5-5", null, null, 2, 900, 0, 0, 0.42m, IsWorker: true));
        RecordedCallAttach.To(pipeline, ledger);

        TurnSpend.Of(pipeline)!.Usd.Should().Be(1.42m);
    }

    [Fact]
    public void Of_NoLedger_ReadsTheTracker()
    {
        var pipeline = new PipelineContext();
        PipelineCostTracker.GetOrCreate(pipeline);

        TurnSpend.Of(pipeline)!.Usd.Should().Be(0m);
    }
}
