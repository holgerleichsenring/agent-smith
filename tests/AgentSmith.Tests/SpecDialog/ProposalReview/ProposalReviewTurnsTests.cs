using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;
using AgentSmith.Tests.TestSupport;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-13-8762i: a later turn carries over what an earlier one cleared. 2026-11-10-18cf: no share bounds a design
/// turn's review — every draft gets its pass; only the run's cost cap stops it, and says so.
/// </summary>
public sealed class ProposalReviewTurnsTests
{
    [Fact]
    public async Task ProposalReview_SevenDraftsWithTheShareLongSpent_EveryDraftGetsARound()
    {
        var ids = Enumerable.Range(1, 7).Select(i => $"2026-10-14-9000{(char)('a' + i)}").ToList();
        var rig = new ProposalReviewRig { Settings = Of(rounds: 4, share: 40, wall: 1800) };
        rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-1017));
        rig.Propose(EpicReply(Yaml(Parent, "The export is rebuilt in seven slices."), ids.Select(id => Yaml(id))));

        await rig.RunAsync();

        rig.Reviewed.Select(i => i.Draft.SpecId).Distinct().Should().BeEquivalentTo(ids,
            "1017 s of 1800 s is past the 40 % share, which a design turn no longer has");
        rig.Outcome.NotReviewed.Should().BeNull();
        rig.Outcome.ReviewRounds.Select(r => r.SpecId).Should().BeEquivalentTo(ids);
    }

    [Fact]
    public async Task ProposalReview_PastTheWallTimeCeiling_ThePassStillRuns()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 4, wall: 1800) };
        rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-3000));
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Should().ContainSingle("time says nothing about a review; only the cost cap stops it");
        rig.Outcome.NotReviewed.Should().BeNull();
    }

    [Fact]
    public async Task ProposalReview_EditTurnAfterNotReviewed_ReviewsOnlyTheUnreviewedDrafts()
    {
        var rig = new ProposalReviewRig();
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            ReviewRounds = [Round(First)],
            NotReviewed = [Second],
        });

        await rig.RunAsync();

        rig.Reviewed.Select(i => i.Draft.SpecId).Distinct().Should().Equal(Second);
        rig.Outcome.ReviewRounds.Count(r => r.SpecId == First).Should().Be(1, "its earlier round is carried");
        rig.Outcome.NotReviewed.Should().BeNull();
    }

    [Fact]
    public async Task ProposalReview_EpicWithContract_SeriesRoundIsNotAParentRound()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Select(r => r.SpecId).Should().OnlyContain(id => id == First || id == Second,
            "the parent is never keyed by a round; the series round has its own token");
    }

    [Fact]
    public async Task ProposalReview_UnchangedParentCarriedOver_SeriesRoundNotDuplicated()
    {
        var rig = new ProposalReviewRig();
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            ReviewRounds = [Round(First), Round(Second), Round(ProposalReviewTokens.Series)],
        });

        await rig.RunAsync();

        rig.Reviewed.Should().BeEmpty("both children are carried, unchanged");
        rig.Outcome.ReviewRounds.Select(r => r.SpecId).Should().Equal(First, Second);
    }

    [Fact]
    public async Task ProposalReview_StoreEditOfTheSwitch_AppliesToTheNextTurn()
    {
        var current = Of(rounds: 1);
        var reader = Reader(() => current);
        var first = new ProposalReviewRig();
        first.Propose(SpecReply(Yaml(Lone)));
        await first.RunAsync(reader);

        current = Of(rounds: 1, designTurn: false);
        var second = new ProposalReviewRig();
        second.Propose(SpecReply(Yaml(Lone)));
        await second.RunAsync(reader);

        first.Reviewed.Should().HaveCount(2);
        second.Reviewed.Should().BeEmpty("the edit is read on the next turn, without a restart");
    }

    [Fact]
    public async Task ProposalReview_TurnWithTemplate_MissingCitedFileIsStillAProblem()
    {
        var template = $"{TemplateScopeName.Prefix}clean";
        var rig = new ProposalReviewRig { RealEvidence = true, Settings = Of(rounds: 1) };
        rig.Pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
        {
            ["shop"] = new EvidenceTreeSandbox(new Dictionary<string, string>()),
            [template] = new EvidenceTreeSandbox(new Dictionary<string, string>
            {
                ["src/Export/ExportHandler.cs"] = string.Join('\n', Enumerable.Repeat("x", 20)),
            }),
        });
        rig.Pipeline.Set(ContextKeys.SandboxRepos,
            (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["shop"] = "shop", [template] = template });
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Outcome.Findings.Should().ContainSingle(f => f.Problem == ProposalEvidenceReview.Problem,
                "the cited file exists only in the template, never in the repository")
            .Which.SpecId.Should().Be(Lone);
    }

    private static ProposalReviewRound Round(string specId) =>
        new(specId, 1, SpecReviewVerdicts.Build, [SpecReviewVerdicts.Build], [], [], []);
}
