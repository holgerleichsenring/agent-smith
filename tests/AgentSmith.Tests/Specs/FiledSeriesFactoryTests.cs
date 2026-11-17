using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Specs.Contract;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-17-5701e: filing marks a draft reviewed in its filed text when the approving turn's last round of it passed
/// (build, build with cuts, or a refusal waiting only on an owner) and it is not in NotReviewed; the contract when the
/// last series round passed and narrowing removed nothing.
/// </summary>
public sealed class FiledSeriesFactoryTests
{
    private const string Series = "2026-11-17-0e0e";

    [Fact]
    public void Under_BuildWithCutsDraftEditedAfterItsReview_IsMarked()
    {
        var edited = Draft("x") with { Yaml = "spec: x\ngoal: \"Do x, cut applied\"" };
        var source = new SpecOutcome(edited) { ReviewRounds = [Round("x", SpecReviewVerdicts.BuildWithCuts)] };

        var filed = ApprovedSetDoubles.SeriesFiling().Under(Series, [edited], source);

        filed.ReviewedClean.Should().Equal(ReviewedText.Of(filed.Drafts[0].SpecId, filed.Drafts[0].Yaml));
    }

    [Fact]
    public void Under_RefusalWaitingOnTheOwner_IsMarked()
    {
        var source = new SpecOutcome(Draft("x"))
        {
            ReviewRounds = [Round("x", SpecReviewVerdicts.Refuse, new SpecReviewFinding("Which store?", "e", "Which store"))],
        };

        ApprovedSetDoubles.SeriesFiling().Under(Series, [Draft("x")], source).ReviewedClean.Should().ContainSingle();
    }

    [Fact]
    public void Under_NotReviewedNoRoundOrStandingRefuse_IsNotMarked()
    {
        var drafts = new[] { Draft("a"), Draft("b"), Draft("c") };
        var source = new EpicOutcome(Draft("p"), drafts)
        {
            ReviewRounds =
            [
                Round("a", SpecReviewVerdicts.Build),
                Round("c", SpecReviewVerdicts.Build),
                Round("c", SpecReviewVerdicts.Refuse, new SpecReviewFinding("The writer never flushes.", "e")),
            ],
            NotReviewed = ["a"],
        };

        var filed = ApprovedSetDoubles.SeriesFiling().Under(Series, drafts, source);

        filed.ReviewedClean.Should().BeEmpty("a's pass did not finish, b has no round, and c's last round refused");
    }

    [Fact]
    public void Under_PassedSeriesRound_MarksTheContract()
    {
        var epic = Epic([Round(ProposalReviewTokens.Series, SpecReviewVerdicts.BuildWithCuts)]);

        var filed = ApprovedSetDoubles.SeriesFiling().Under(Series, epic.Children, epic);

        filed.ReviewedClean.Should().Contain(ReviewedText.Of(filed.Contract!));
    }

    [Fact]
    public void Under_RefusedSeriesRoundOrSeriesNotReviewed_LeavesTheContractUnmarked()
    {
        var refused = Epic([Round(ProposalReviewTokens.Series, SpecReviewVerdicts.Refuse, new SpecReviewFinding("f", "e"))]);
        var unfinished = Epic([Round(ProposalReviewTokens.Series, SpecReviewVerdicts.Build)]) with
        {
            NotReviewed = [ProposalReviewTokens.Series],
        };

        foreach (var epic in new[] { refused, unfinished })
        {
            var filed = ApprovedSetDoubles.SeriesFiling().Under(Series, epic.Children, epic);
            ReviewedText.Covers(filed.ReviewedClean, filed.Contract!).Should().BeFalse();
        }
    }

    [Fact]
    public void Under_NarrowedContract_IsNotMarked()
    {
        var epic = Epic([Round(ProposalReviewTokens.Series, SpecReviewVerdicts.Build)]);

        var filed = ApprovedSetDoubles.SeriesFiling().Under(Series, [epic.Children[1]], epic);

        ReviewedText.Covers(filed.ReviewedClean, filed.Contract!).Should().BeFalse("the reviewed contract had an entry narrowing removed");
    }

    [Fact]
    public void LastRoundPassed_ReadsOnlyTheLastRoundUnderTheId()
    {
        IReadOnlyList<ProposalReviewRound> rounds =
            [Round("x", SpecReviewVerdicts.Refuse, new SpecReviewFinding("f", "e")), Round("x", SpecReviewVerdicts.Build)];

        ProposalReviewText.LastRoundPassed(rounds, "x").Should().BeTrue();
        ProposalReviewText.LastRoundPassed(rounds, "y").Should().BeFalse("no round is no pass");
        ProposalReviewText.LastRoundPassed([.. rounds.Reverse()], "x").Should().BeFalse();
    }

    private static EpicOutcome Epic(IReadOnlyList<ProposalReviewRound> rounds) =>
        new(Draft("p"), [Draft("c-1"), Draft("c-2")], SeriesContractFixtures.Of(
            names: [new("SpecReviewRound", "event", 87, "c-1", ["c-2"])],
            areas: [new("c-1", ["src/A.cs"]), new("c-2", ["src/A.cs"])],
            budgets: [new("src/A.cs", "c-1", 100), new("src/A.cs", "c-2", 110)]))
        {
            ReviewRounds = rounds,
        };

    private static ProposalReviewRound Round(string id, string verdict, params SpecReviewFinding[] findings) =>
        new(id, 1, verdict, [], [], findings, []);

    private static SpecDraft Draft(string id) =>
        new(id, $"Do {id}", $"spec: {id}\ngoal: \"Do {id}\"", []) { Done = ["It is done."] };
}
