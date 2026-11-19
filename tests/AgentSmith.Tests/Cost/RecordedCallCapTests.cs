using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Cost;

/// <summary>2026-11-17-b34ea: the cap's money arm counts the run's recorded calls, worker-reported cost included.</summary>
public sealed class RecordedCallCapTests
{
    private static readonly CostCapValues Cap = new() { Usd = 1.00m, Tokens = 100_000_000 };

    private static WorkerCallAccounting Worker(decimal cost) =>
        new("claude-opus-5-5", InputTokens: 10, OutputTokens: 10, CacheReadTokens: 0, CacheCreationTokens: 0,
            ReportedCostUsd: cost, CliTurns: 1);

    private static RecordedCall Recorded(decimal cost) => new("claude-opus-5-5", null, null, 1, 1, 0, 0, cost, IsWorker: true);

    [Fact]
    public void WorkerReportedCost_OverTheCap_ExhaustsIt()
    {
        var tracker = new PipelineCostTracker(config: null, costCap: Cap);
        tracker.TrackWorkerCall(Worker(0.60m));
        tracker.IsBudgetExhausted.Should().BeFalse();

        tracker.TrackWorkerCall(Worker(0.60m));

        tracker.BudgetCostUsd.Should().Be(1.20m);
        tracker.IsBudgetExhausted.Should().BeTrue("a worker run over its budget stops like any other");
        tracker.EstimateCostUsd().Should().Be(0m, "the priced estimate result.md reads is unchanged");
    }

    [Fact]
    public void RecordedLedger_AboveTheTrackersFigure_ExhaustsIt_AndBelowItLowersNothing()
    {
        var tracker = new PipelineCostTracker(config: null, costCap: Cap);
        var ledger = new RecordedCallLedger();
        tracker.CountRecordedCalls(ledger);
        tracker.TrackWorkerCall(Worker(0.30m));

        ledger.Add(Recorded(0.10m));
        tracker.BudgetCostUsd.Should().Be(0.30m, "the ledger may miss what the tracker saw");

        ledger.Add(Recorded(1.40m));
        tracker.BudgetCostUsd.Should().Be(1.50m);
        tracker.IsBudgetExhausted.Should().BeTrue();
    }

    [Fact]
    public void NoCap_NeverExhausts()
    {
        var tracker = new PipelineCostTracker();
        tracker.TrackWorkerCall(Worker(500m));
        tracker.IsBudgetExhausted.Should().BeFalse();
    }

    [Fact]
    public void Ledger_IsCounted_WhetherTheTrackerComesBeforeOrAfterTheAttach()
    {
        var before = new PipelineContext();
        before.Set("PipelineCostCap", Cap);
        var early = PipelineCostTracker.GetOrCreate(before);
        var first = new RecordedCallLedger();
        RecordedCallAttach.To(before, first);
        first.Add(Recorded(2m));
        early.IsBudgetExhausted.Should().BeTrue("a tracker the prologue created counts the ledger attached later");

        var after = new PipelineContext();
        after.Set("PipelineCostCap", Cap);
        var second = new RecordedCallLedger();
        RecordedCallAttach.To(after, second);
        second.Add(Recorded(2m));
        PipelineCostTracker.GetOrCreate(after).IsBudgetExhausted.Should().BeTrue();
    }

    [Fact]
    public void StopSentence_NamesTheKeyAndTheWorkerIncludedSpend()
    {
        var tracker = new PipelineCostTracker(config: null, costCap: Cap);
        tracker.TrackWorkerCall(Worker(1.25m));

        CostCapStop.Describe(tracker).Should().Contain("$1.00").And.Contain("$1.25")
            .And.Contain("worker-reported cost").And.Contain("pipeline_cost_cap");
    }

    [Fact]
    public void MasterFence_TripsOnAPassOfWorkerResponses()
    {
        var context = MasterHandlerFixture.BuildContext("master");
        context.Pipeline.Set("PipelineCostCap", Cap);
        var tracker = new PipelineCostTracker(config: null, costCap: Cap);
        var ledger = () => new ProgressLedger([]);
        var hooks = MasterLoopHooksFactory.Build(context, tracker, ledger,
            new LogDecisionToolHost(Mock.Of<IDecisionLogger>(), null));

        hooks.RecordIterationUsage!(Worker(0.70m).AttachTo(new ChatResponse(new ChatMessage(ChatRole.Assistant, "a"))));
        hooks.IsBudgetExhausted!().Should().BeFalse();
        hooks.RecordIterationUsage!(Worker(0.70m).AttachTo(new ChatResponse(new ChatMessage(ChatRole.Assistant, "b"))));

        hooks.IsBudgetExhausted!().Should().BeTrue();
        hooks.RenderBudgetStop!().Should().Contain("$1.40");
    }
}
