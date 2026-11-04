using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Loop;

public sealed class SkillCallCostTrackingTests
{
    private static ChatResponse MakeResponse(int input, int output)
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output }
        };
        return response;
    }

    [Fact]
    public void BeginCall_AccumulatesTokensInPerSkillBreakdown()
    {
        var tracker = new PipelineCostTracker();
        using (var _ = tracker.BeginCall("skill-a", "planner", SkillExecutionPhase.Plan))
        {
            tracker.Track(MakeResponse(100, 50));
            tracker.Track(MakeResponse(200, 75));
        }

        tracker.PerSkillBreakdown.Should().HaveCount(1);
        tracker.PerSkillBreakdown[0].SkillName.Should().Be("skill-a");
        tracker.PerSkillBreakdown[0].InputTokens.Should().Be(300);
        tracker.PerSkillBreakdown[0].OutputTokens.Should().Be(125);
    }

    [Fact]
    public void BeginCall_PreservesPipelineTotals()
    {
        var tracker = new PipelineCostTracker();
        using (var _ = tracker.BeginCall("skill-a", "planner", SkillExecutionPhase.Plan))
            tracker.Track(MakeResponse(100, 50));

        tracker.TotalInputTokens.Should().Be(100);
        tracker.TotalOutputTokens.Should().Be(50);
        tracker.CallCount.Should().Be(1);
    }

    [Fact]
    public void BeginCall_NestedCallsThrow()
    {
        var tracker = new PipelineCostTracker();
        using var _ = tracker.BeginCall("skill-a", "planner", SkillExecutionPhase.Plan);

        Action act = () => tracker.BeginCall("skill-b", "judge", SkillExecutionPhase.Review);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*nest*");
    }

    [Fact]
    public void PerSkillBreakdown_OrderedByStartTime()
    {
        var tracker = new PipelineCostTracker();

        using (var _ = tracker.BeginCall("skill-1", "planner", SkillExecutionPhase.Plan))
            tracker.Track(MakeResponse(10, 5));
        Thread.Sleep(5);
        using (var _ = tracker.BeginCall("skill-2", "judge", SkillExecutionPhase.Review))
            tracker.Track(MakeResponse(20, 10));

        var breakdown = tracker.PerSkillBreakdown;
        breakdown.Should().HaveCount(2);
        breakdown[0].SkillName.Should().Be("skill-1");
        breakdown[1].SkillName.Should().Be("skill-2");
        breakdown[0].StartedAt.Should().BeOnOrBefore(breakdown[1].StartedAt);
    }

    [Fact]
    public void CallCostRecord_DurationMatchesScopeLifetime()
    {
        var tracker = new PipelineCostTracker();
        using (var _ = tracker.BeginCall("skill-a", "planner", SkillExecutionPhase.Plan))
            Thread.Sleep(20);

        var record = tracker.PerSkillBreakdown.Single();
        record.DurationMs.Should().BeGreaterThanOrEqualTo(15);
    }

    // 2026-11-02-2b26b: the parallel attack reviewers open a scope each on one tracker; neither may throw.
    [Fact]
    public async Task BeginCall_TwoConcurrentFlows_BothOpenAndAttributeSeparately()
    {
        var tracker = new PipelineCostTracker();
        var bothOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = 0;

        async Task CallAsync(string skill, int input)
        {
            await Task.Yield();
            using var _ = tracker.BeginCall(skill, skill, SkillExecutionPhase.Plan);
            if (Interlocked.Increment(ref opened) == 2) bothOpen.TrySetResult();
            await bothOpen.Task.OrHang("both scopes open at once");
            tracker.Track(MakeResponse(input, 1));
        }

        await Task.WhenAll(CallAsync("reviewer-0", 100), CallAsync("reviewer-1", 200));

        tracker.PerSkillBreakdown.Select(r => (r.SkillName, r.InputTokens)).Should().BeEquivalentTo(
            new[] { ("reviewer-0", 100L), ("reviewer-1", 200L) });
    }

    [Fact]
    public void OpenScopeOnOneTracker_DoesNotBlockAnotherTrackerInTheSameFlow()
    {
        var pipeline = new PipelineCostTracker();
        var turn = new PipelineCostTracker();
        using var _ = pipeline.BeginCall("skill-a", "planner", SkillExecutionPhase.Plan);

        using (turn.BeginCall("skill-b", "judge", SkillExecutionPhase.Review))
            turn.Track(MakeResponse(30, 3));

        turn.PerSkillBreakdown.Single().InputTokens.Should().Be(30);
    }
}
