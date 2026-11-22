using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Rework.Stacked;

/// <summary>
/// 2026-11-17-9a39a: a prerequisite whose run ended green wakes each ready dependent once under on_green_push; on_merge,
/// a red attempt and a dependent that already ran wake nothing.
/// </summary>
public sealed class StackedDependentsWakeTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly InMemorySpecApprovalStore _approvals = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true }; // 9719d: the set reads loop LatestAsync
    private readonly Mock<IReworkNudges> _nudges = new();

    public StackedDependentsWakeTests()
    {
        _facts.Edges.Add(Edge("42", "41", startsWhenMet: true));
        _approvals.SaveAsync(ApprovedSets.Record("jira-42", DateTimeOffset.UnixEpoch, tracker: Tracker), default).GetAwaiter().GetResult();
        _attempts.Setup(a => a.LatestAsync(Project, "41", null, default)).ReturnsAsync(Green());
    }

    [Fact]
    public async Task WakeAsync_GreenPrerequisite_NudgesTheReadyDependentOnce()
    {
        var woken = await Wake().WakeAsync(Of(RequiresStart.OnGreenPush), "41", Green(), default);

        woken.Should().Be(1);
        _nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r =>
            r.TicketId == "42" && r.Origin == ReworkNudgeOrigin.RequirementsMet), default), Times.Once);
    }

    [Fact]
    public async Task WakeAsync_OnMerge_NudgesNothing() =>
        (await Wake().WakeAsync(Of(RequiresStart.OnMerge), "41", Green(), default)).Should().Be(0);

    [Fact]
    public async Task WakeAsync_ARedAttempt_NudgesNothing() =>
        (await Wake().WakeAsync(Of(RequiresStart.OnGreenPush), "41", Green() with { Status = RunStatuses.Failed }, default))
            .Should().Be(0);

    [Fact]
    public async Task WakeAsync_ADependentThatRanAlready_IsNotNudged()
    {
        _attempts.Setup(a => a.LatestAsync(Project, "42", null, default)).ReturnsAsync(Green());

        (await Wake().WakeAsync(Of(RequiresStart.OnGreenPush), "41", Green(), default)).Should().Be(0);
    }

    private StackedDependentsWake Wake() =>
        new(_facts, _facts, new UnmetTicketRequirements(_facts, _facts, _approvals, new GreenPrerequisites(_facts, _attempts.Object, _facts)),
            _attempts.Object, Mock.Of<IActiveRunLease>(), _nudges.Object, NullLogger<StackedDependentsWake>.Instance);

    private static PreviousAttempt Green() =>
        new("run-41", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true) { OpenedPullRequest = true };

    private static ResolvedProject Of(RequiresStart mode) => new()
    {
        Name = Project, Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.Jira }, RequiresStart = mode,
    };
}
