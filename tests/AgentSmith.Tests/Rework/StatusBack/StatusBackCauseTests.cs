using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Rework.StatusBack;

/// <summary>2026-10-08-2123: a person's move back appends only to a fully executed set, and only with feedback since.</summary>
public sealed class StatusBackCauseTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    private static SpecPhase Phase(string id) => new(
        new PhaseDraft(id, $"Goal {id}", $"spec: {id}\ngoal: \"Goal {id}\"", []) { Done = [$"Done {id}."] }, id, string.Empty, []);

    private static SpecSet Set(int phases, int executed) => new("k", [.. Enumerable.Range(0, phases).Select(i => Phase($"p{i}"))],
        SpecAccounting.Empty, [new SpecRevision(1, SpecRevisionCause.Initial, Start)], SpecSource.BranchArtifact,
        ExecutedPhaseIds: [.. Enumerable.Range(0, executed).Select(i => $"p{i}")]);

    private static Ticket Ticket() => new(new TicketId("1"), "t", "text", null, "To Do", "jira", []);

    private static PipelineContext Pipeline(bool moved, params TicketComment[] comments)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("run-1", "success", Start, true) { ActsReadAt = Start.AddMinutes(1) });
        if (moved) pipeline.Set(ContextKeys.StatusBackAct, new ReworkAct("alice", Start.AddHours(2), ReworkChannel.Status));
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments,
            [new TicketComment("Agent Smith", Start.AddMinutes(-5), $"## Agent Smith — {SpecSetComment.CutMarker}"), .. comments]);
        return pipeline;
    }

    private static string Cause(SpecSet set, PipelineContext pipeline) =>
        SpecRevisionCause.For(new SpecSetReadResult(set, "sha"), new SpecSetPointer("k", "primary", "sha", 1), Ticket(), pipeline);

    private static TicketComment Asked() => new("bob", Start.AddHours(1), "the totals are still wrong");

    [Fact]
    public void Cause_CommentThenMove_IsStatusBack() =>
        Cause(Set(2, 2), Pipeline(moved: true, Asked())).Should().Be(SpecRevisionCause.StatusBack);

    [Fact]
    public void Cause_MoveWithoutFeedback_KeepsExistingOrder() =>
        Cause(Set(2, 2), Pipeline(moved: true)).Should().Be(SpecRevisionCause.Retrigger);

    [Fact]
    public void Cause_MoveWithTailAndComment_IsComment() =>
        Cause(Set(3, 2), Pipeline(moved: true, Asked())).Should().Be(SpecRevisionCause.Comment);

    [Fact]
    public void NeedsModel_StatusBackWithUnexecutedTail_False()
    {
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.StatusBack, Set(3, 2)).Should().BeFalse();
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.StatusBack, Set(2, 2)).Should().BeTrue();
    }
}
