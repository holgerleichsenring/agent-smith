using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Loop;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Cost;

/// <summary>
/// 2026-09-22-7c41a: the spend a run made before it parked crosses the checkpoint as a
/// plain snapshot of RAW buckets and seeds the tracker the relaunch creates — so the cap
/// is compared against the whole run, a second park compounds, and a reader that stops on
/// the cap can name both the ceiling and what crossed it.
/// </summary>
public sealed class ResumedSpendTests
{
    private static readonly CostCapValues Cap = new() { Usd = 100m, Tokens = 1_000_000 };

    [Fact]
    public void Resume_TheSpendBeforeThePark_CountsTowardsTheCapAfterIt()
    {
        var parked = new PipelineCostTracker(config: null, costCap: Cap);
        parked.Track(Response(input: 900_000, output: 50_000));
        parked.IsBudgetExhausted.Should().BeFalse("the parked segment was still under the cap");

        var resumed = PipelineCostTracker.GetOrCreate(Resumed(parked.CaptureSpend()));
        resumed.EffectiveBudgetTokens.Should().Be(950_000);
        resumed.Track(Response(input: 60_000, output: 0));

        resumed.IsBudgetExhausted.Should().BeTrue(
            "1,010,000 tokens across both segments is over the cap; the segment alone is not");
    }

    [Fact]
    public void Resume_ARunParkedTwice_CarriesTheWholeRunsSpend()
    {
        var first = new PipelineCostTracker(config: null, costCap: Cap);
        first.Track(Response(input: 300_000, output: 0));

        var second = PipelineCostTracker.GetOrCreate(Resumed(first.CaptureSpend()));
        second.Track(Response(input: 400_000, output: 0));
        var third = PipelineCostTracker.GetOrCreate(Resumed(second.CaptureSpend()));

        third.EffectiveBudgetTokens.Should().Be(700_000, "the snapshot is taken AFTER seeding");
        third.TotalInputTokens.Should().Be(700_000);
    }

    // The worker-CLI transport binds the token arm and deliberately not the money arm, and
    // seeding it must not invent a call the resumed segment never placed.
    [Fact]
    public void Resume_WorkerTransportVolume_CrossesWithoutInventingACall()
    {
        var parked = new PipelineCostTracker(config: null, costCap: Cap);
        parked.TrackWorkerCall(new WorkerCallAccounting(
            "sonnet", InputTokens: 500_000, OutputTokens: 100_000,
            CacheReadTokens: 0, CacheCreationTokens: 0, ReportedCostUsd: 0.5m, CliTurns: 1));

        var resumed = PipelineCostTracker.GetOrCreate(Resumed(parked.CaptureSpend()));

        resumed.EffectiveBudgetTokens.Should().Be(600_000);
        resumed.WorkerCalls.CallCount.Should().Be(0, "this segment placed no worker call");
        resumed.BuildSummary().Should().BeNull("a segment with no call of its own renders none");
    }

    [Fact]
    public async Task Resume_ARunAlreadyOverItsCap_StopsAtTheFirstReaderNamingTheCap()
    {
        var parked = new PipelineCostTracker(config: null, costCap: Cap);
        parked.Track(Response(input: 1_200_000, output: 0));
        var (runtime, _, factory) = RuntimeBuilder.Build(new Mock<IChatClient>().Object);
        var resumed = PipelineCostTracker.GetOrCreate(Resumed(parked.CaptureSpend()));

        var result = await runtime.ExecuteAsync(
            RuntimeBuilder.MakeRequest(), resumed, CancellationToken.None);

        result.Outcome.Should().Be(SkillCallOutcome.Incomplete);
        result.FailureReason.Should().Contain("$100.00 / 1,000,000 tokens")
            .And.Contain("1,200,000 cache-weighted tokens");
        factory.LastMaxIterations.Should().BeNull("the skill short-circuited before any call");
    }

    [Fact]
    public void MoneyFence_TheStopMessage_NamesTheCapAndTheSpend()
    {
        var context = MasterHandlerFixture.BuildContext("master");
        context.Pipeline.Set("PipelineCostCap", Cap);
        var tracker = new PipelineCostTracker(config: null, costCap: Cap);
        tracker.Track(Response(input: 400_000, output: 0));
        var ledger = () => new ProgressLedger([]);

        var hooks = MasterLoopHooksFactory.Build(
            context, tracker, ledger,
            new LogDecisionToolHost(Mock.Of<IDecisionLogger>(), null),
            new VerdictOwed("master", ledger, () => 0m, 3, NullLogger.Instance));

        hooks.RenderBudgetStop.Should().NotBeNull();
        hooks.RenderBudgetStop!().Should().Contain("$100.00 / 1,000,000 tokens")
            .And.Contain("400,000 cache-weighted tokens");
    }

    // A fresh context carrying only what a relaunch seeds plus the snapshot the checkpoint
    // brought back — the one place a prior segment's spend is read.
    private static PipelineContext Resumed(PriorSpendSnapshot prior)
    {
        var pipeline = new PipelineContext();
        pipeline.Set("PipelineCostCap", Cap);
        pipeline.Set(ContextKeys.PriorSpend, prior);
        return pipeline;
    }

    private static ChatResponse Response(int input, int output) =>
        new(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            ModelId = "gpt-4.1",
            Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output },
        };
}
