using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-13-8762k: a design turn's series round — findings only, one round under the series token, never the
/// parent's id, and never approvable while the contract is unreviewed.
/// </summary>
public sealed class ProposalSeriesRoundTests
{
    private const string Quote = "Write the orders through the buffered";
    private const string Entry = "series contract: name: event 'OrderExported' = 87, owner " + First;

    [Fact]
    public async Task ProposalReview_EpicWithContract_AddsSeriesContractFindingsWithoutReprompt()
    {
        var rig = Contracted(CutOnSecond());

        await rig.RunAsync();

        rig.SeriesReviewed.Should().ContainSingle().Which.Members.Select(m => m.Draft.SpecId).Should().Equal(First, Second);
        rig.Outcome.Findings.Should().ContainSingle(f => f.SpecId == Second && f.Problem == ProposalSeriesRound.Problem)
            .Which.Should().Match<ProposalFinding>(f => f.Quote == Quote && f.Evidence == Entry && f.Why.Contains("export queue"));
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Parent && f.Problem == ProposalSeriesRound.Problem
            && f.Why.Contains(SpecReviewVerdicts.BuildWithCuts));
        rig.DraftOf(Second).Yaml.Should().Contain("buffered file writer");
    }

    [Fact]
    public async Task ProposalReview_ShareSpent_SeriesContractStillReviewed()
    {
        var rig = Contracted(CutOnSecond());
        rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-1000));

        await rig.RunAsync();

        rig.SeriesReviewed.Should().ContainSingle("a design turn's review has no share (2026-11-10-18cf)");
        rig.Outcome.ReviewRounds.Should().Contain(r => r.SpecId == ProposalReviewTokens.Series);
    }

    [Fact]
    public async Task ProposalReview_PastTheWallTimeCeiling_SeriesContractStillReviewed()
    {
        var rig = Contracted(CutOnSecond());
        rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-5000));

        await rig.RunAsync();

        rig.SeriesReviewed.Should().ContainSingle("a design turn's review has no wall-time rule (2026-11-10-18cf)");
        rig.Outcome.ReviewRounds.Should().Contain(r => r.SpecId == ProposalReviewTokens.Series);
    }

    [Fact]
    public async Task ProposalReview_SeriesCallFails_ListsContractInNotReviewed()
    {
        var rig = Contracted(_ => SeriesContractReview.Failed("the reviewer call failed or its answer could not be read"));

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().Equal(ProposalReviewTokens.Series);
    }

    [Fact]
    public async Task ProposalReview_EpicWithContract_RecordsOneSeriesRoundUnderTheSeriesToken()
    {
        var rig = Contracted(CutOnSecond());

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Where(r => r.SpecId == ProposalReviewTokens.Series).Should().ContainSingle()
            .Which.Should().Match<ProposalReviewRound>(r => r.Round == 1 && r.Verdict == SpecReviewVerdicts.BuildWithCuts
                && r.Cuts.Single().SpecId == Second && r.ReviewerVerdicts.Single() == SpecReviewVerdicts.BuildWithCuts);
    }

    [Fact]
    public async Task ProposalReview_EpicWithContract_SeriesRoundIsNotAParentRound()
    {
        var rig = Contracted(CutOnSecond());

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Should().NotContain(r => r.SpecId == Parent);
    }

    [Fact]
    public async Task ProposalReview_UnchangedParentCarriedOver_SeriesRoundNotDuplicated()
    {
        var before = Contracted(CutOnSecond());
        await before.RunAsync();
        var rig = Contracted(CutOnSecond());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, before.Outcome);

        await rig.RunAsync();

        rig.Outcome.ReviewRounds.Count(r => r.SpecId == ProposalReviewTokens.Series).Should().Be(1);
        rig.SeriesReviewed.Should().ContainSingle("the series round is taken fresh each turn");
    }

    [Fact]
    public async Task SettledParent_SeriesRoundNotTakenAgain()
    {
        var rig = new ProposalReviewRig
        {
            OnSeriesReview = CutOnSecond(),
            OnReview = input => input.Draft.SpecId == Second && input.Draft.Yaml.Contains("buffered") ? Refuse("not buffered") : Build(),
        };
        rig.Propose(ContractEpic());
        await rig.RunAsync();
        var settled = rig.Outcome.ReviewRounds.Single(r => r.SpecId == ProposalReviewTokens.Series);
        rig.Propose(ContractEpic(Yaml(Second, step: "Write the orders through the export queue.", requires: [First])));

        await rig.RunAsync();

        rig.SeriesReviewed.Should().ContainSingle("the series round passed this turn");
        rig.Outcome.ReviewRounds.Where(r => r.SpecId == ProposalReviewTokens.Series).Should().ContainSingle()
            .Which.Should().BeSameAs(settled);
        rig.Outcome.ReviewRounds.Should().Contain(r => r.SpecId == Second && r.Verdict == SpecReviewVerdicts.Build);
    }

    [Fact]
    public async Task ProposalReview_LoneSpec_TakesNoSeriesRound()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.SeriesReviewed.Should().BeEmpty();
        rig.Outcome.ReviewRounds.Should().NotContain(r => r.SpecId == ProposalReviewTokens.Series);
    }

    [Fact]
    public async Task ProposalReview_EpicWithoutContract_TakesNoSeriesRound()
    {
        var rig = new ProposalReviewRig();
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.SeriesReviewed.Should().BeEmpty();
    }

    private static Func<SeriesContractInput, SeriesContractReview> CutOnSecond() => _ => new SeriesContractReview(
        new SpecAttackResult(SpecReviewVerdicts.BuildWithCuts,
            [new SpecReviewCut(SpecRegisterNames.Steps, Quote, "Write the orders through the export queue", Entry, Second)], []), []);

    private static ProposalReviewRig Contracted(Func<SeriesContractInput, SeriesContractReview> answer)
    {
        var rig = new ProposalReviewRig { OnSeriesReview = answer };
        rig.Propose(ContractEpic());
        ((EpicOutcome)rig.Outcome).Contract.Should().NotBeNull("the fixture's epic carries a contract");
        return rig;
    }
}
