using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-f114: the cause, the amendment rule, the approval and the prompt for a rework
/// of a set with nothing left to run.</summary>
public sealed class ReworkAppendRuleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly SpecApproval Prior = new(Now.AddDays(-1), "conv-7", "alice");

    private static DerivedSpec Spec(string id) => new(
        new SpecDraft(id, $"Goal {id}", $"spec: {id}\ngoal: \"Goal {id}\"", []) { Done = [$"Done {id}."] }, id, string.Empty, []);

    private static SpecSet Executed(int specs, SpecApproval? approval = null, string cause = SpecRevisionCause.Initial)
    {
        var ids = Enumerable.Range(0, specs).Select(i => $"p{i}").ToList();
        return new SpecSet("k", [.. ids.Select(Spec)], SpecAccounting.Empty, [new SpecRevision(1, cause, Now)],
            SpecSource.BranchArtifact, ExecutedSpecIds: ids) { Approval = approval };
    }

    private static Ticket Ticket() => new(new TicketId("1"), "t", "text", null, "open", "azdo", []);

    private static PipelineContext WithAct(ReworkChannel channel, bool resuming = false)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ReworkAct, new ReworkAct("alice", Now, channel));
        if (resuming) pipeline.Set(ContextKeys.ResumeCheckpoint, "cp");
        return pipeline;
    }

    private static string CauseFor(PipelineContext pipeline) => SpecRevisionCause.For(
        new SpecSetReadResult(Executed(2), "sha"), new SpecSetPointer("k", "primary", "sha", 1), Ticket(), pipeline);

    [Fact]
    public void Cause_PullRequestAct_IsRework() => CauseFor(WithAct(ReworkChannel.PullRequest)).Should().Be(SpecRevisionCause.Rework);

    [Fact]
    public void Cause_PullRequestActWhileResuming_IsResume() =>
        CauseFor(WithAct(ReworkChannel.PullRequest, resuming: true)).Should().Be(SpecRevisionCause.Resume);

    [Fact]
    public void Cause_TicketChannelAct_StaysComment() => CauseFor(WithAct(ReworkChannel.Ticket)).Should().NotBe(SpecRevisionCause.Rework);

    [Fact]
    public void NeedsModel_ReworkOnApprovedSet_True() =>
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Rework, Executed(2, Prior), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().BeTrue();

    [Fact]
    public void NeedsModel_FullSetAtCap_False()
    {
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Comment, Executed(AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults.MaxSpecs), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().BeFalse();
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Rework, Executed(AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults.MaxSpecs), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().BeFalse();
    }

    [Fact]
    public void Decide_ReworkOnApprovedSet_KeepsPriorApproval() =>
        Decide(Executed(2, Prior)).Approval.Should().Be(Prior);

    [Fact]
    public void Decide_ReworkOnUnapprovedSet_RecordsNone() => Decide(Executed(2)).Approval.Should().BeNull();

    private static SpecSourceResolver.Decision Decide(SpecSet set) =>
        new SpecSourceResolver(new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance)
            .Decide(SpecSetOnBranch.Answered(new SpecSetReadResult(set, "sha")), Ticket(),
                new SpecSetPointer("k", "primary", "sha", 1), WithAct(ReworkChannel.PullRequest), "k",
                AgentSmith.Contracts.Tickets.TicketLabelVocabulary.ForOptional(null));

    [Fact]
    public void PreviousCut_FullyExecutedTicketEdit_RendersAppendWithRoom()
    {
        var rendered = PreviousCutPromptSection.Render(Executed(3), SpecRevisionCause.TicketEdit, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults);

        rendered.Should().Contain("ADD new specs after them").And.Contain($"at most {AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults.AppendRoom(3)} new spec(s)");
    }

    [Fact]
    public void PreviousCut_Rework_NamesThePullRequestReview() =>
        PreviousCutPromptSection.Render(Executed(2), SpecRevisionCause.Rework, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().Contain("pull request review section");

    [Fact]
    public void Notice_CommentAtCap_SaysFull()
    {
        var set = Executed(AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults.MaxSpecs) with
        {
            Revisions = [new SpecRevision(1, SpecRevisionCause.Initial, Now), new SpecRevision(2, SpecRevisionCause.Comment, Now)],
        };

        SpecRecutNotice.Render(set, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().Contain("nothing was added").And.NotContain("cut again");
    }

    [Fact]
    public void Parse_AppendReplyShorterThanHead_Refused()
    {
        const string reply = """{ "specs": [ { "goal": "only one", "done": ["x"], "carries": [] } ] }""";

        var parsed = AgentSmith.Tests.Specs.DerivationTestParsers.Real().Parse(
            reply, "k", "2026-10-06-0a0a", "1", [], SpecSource.Derived, Executed(2).Specs);

        parsed.Derivation.Should().BeNull();
        parsed.Error.Should().Contain("2 already ran");
    }
}
