using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: the run's one series round over the cleared prefix — after the per-spec loop, never moving its
/// stop, its cuts landing only on members reviewed in this attempt, one revision before its event, and a standing
/// REFUSE asked through the step's one decision.
/// </summary>
public sealed class SeriesContractRoundTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the export queue.";
    private static readonly (string, string) Edit = (Step, NewStep);

    [Fact]
    public async Task SeriesContractRound_SetWithContract_PublishesOneSeriesRoundAfterTheRevision()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs())).Revisions(Edit);
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.BuildWithCuts, SeriesCut(Second));

        var result = await rig.ExecuteAsync(Settings());

        var series = rig.Events.Events.OfType<SpecReviewRoundEvent>().Where(e => e.Scope == SpecReviewScopes.Series).ToList();
        series.Should().ContainSingle().Which.Should().Match<SpecReviewRoundEvent>(e =>
            e.SpecId == null && e.Round == 1 && e.Reviewer == 0 && e.RevisionNumber == rig.Set.Current.Number);
        rig.Set.Current.Cause.Should().Be(SpecRevisionCause.SeriesContractReview);
        rig.Set.Specs[1].Draft.Yaml.Should().Contain(NewStep);
        rig.Events.Events.ToList().IndexOf(series[0]).Should().BeGreaterThanOrEqualTo(rig.EventsAtPublish[^1],
            "the round's row carries the revision, so it follows it");
        result.Message.Should().Contain("series contract round over 2 cleared member(s): " + SpecReviewVerdicts.BuildWithCuts + ", revised " + Second);
    }

    [Fact]
    public async Task SeriesContractRound_ShareSpent_ReviewsOnlyTheClearedPrefix()
    {
        var rig = ShareSpentAfterTheFirst(Contracted(ReviewExhaustionTests.ThreeSpecs()));

        await rig.ExecuteAsync(Settings());

        rig.SeriesCalls.Should().ContainSingle().Which.Input.Members.Select(m => (m.Draft.SpecId, m.Status)).Should().Equal(
            (First, SeriesContractMember.Cleared), (Second, SeriesContractMember.NotCleared),
            (ReviewExhaustionTests.Third, SeriesContractMember.NotCleared));
        rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop).BeforeSpecId.Should().Be(Second, "the round never moves g's stop");
    }

    [Fact]
    public async Task ReviewSpecs_TailReviewExceedsShare_BuildsTheClearedPrefixEveryAttempt()
    {
        var first = ShareSpentAfterTheFirst(Contracted(ReviewExhaustionTests.ThreeSpecs()));
        await first.ExecuteAsync(Settings());
        (await Built(first)).Should().Equal(First);

        var second = ShareSpentAfterTheFirst(Contracted(ReviewExhaustionTests.ThreeSpecs()) with { Executed = [First] });
        await second.ExecuteAsync(Settings());

        (await Built(second)).Should().Equal(Second);
        second.SeriesCalls.Should().ContainSingle("each attempt reviews its own cleared prefix");
    }

    [Fact]
    public async Task SeriesContractRound_NoContract_TakesNoRoundAndSaysSo()
    {
        var rig = new ReviewLoopRig();

        var result = await rig.ExecuteAsync(Settings());

        result.Message.Should().Contain("series contract round skipped: the set has no series contract");
        rig.SeriesCalls.Should().BeEmpty();
        rig.Events.Events.OfType<SpecReviewRoundEvent>().Should().NotContain(e => e.Scope == SpecReviewScopes.Series);
    }

    [Fact]
    public async Task SeriesContractRound_ApprovedSetUnchanged_TakesNoRound()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs()));

        var round = await rig.SeriesRound().RunAsync(rig.Run(), reviewed: [], CancellationToken.None);

        round.Line.Should().Be("series contract round skipped: no cleared member was reviewed in this attempt (still the approved text)");
        rig.SeriesCalls.Should().BeEmpty();
        rig.Events.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task SeriesContractRound_SingleUnexecutedMember_TakesNoRound()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs()) with { Executed = [First] });

        var result = await rig.ExecuteAsync(Settings());

        result.Message.Should().Contain("series contract round skipped: the set has a single unexecuted member");
        rig.SeriesCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SeriesContractRound_CutsOnTwoMembers_PublishOneRevision()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs())).Revisions(Edit, Edit);
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.BuildWithCuts, SeriesCut(First), SeriesCut(Second));

        await rig.ExecuteAsync(Settings());

        rig.Published.Where(p => p.Current.Cause == SpecRevisionCause.SeriesContractReview).Should().ContainSingle()
            .Which.Specs.Should().OnlyContain(spec => spec.Draft.Yaml.Contains(NewStep));
    }

    [Fact]
    public async Task SeriesContractRound_CutOnExecutedMember_IsDropped()
    {
        var rig = new ReviewLoopRig(Contracted(ReviewExhaustionTests.ThreeSpecs()) with { Executed = [First] }).Revisions(Edit);
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.BuildWithCuts, SeriesCut(First));

        await rig.ExecuteAsync(Settings());

        var round = SeriesEvent(rig);
        round.Cuts.Should().BeEmpty();
        round.EvidenceProblems.Should().ContainSingle().Which.Problem.Should().Contain("executed");
        rig.Published.Should().NotContain(p => p.Current.Cause == SpecRevisionCause.SeriesContractReview);
    }

    [Fact]
    public async Task SeriesContractRound_CutPastTheStop_IsDropped()
    {
        var rig = ShareSpentAfterTheFirst(Contracted(ReviewExhaustionTests.ThreeSpecs())).Revisions(Edit);
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.BuildWithCuts, SeriesCut(ReviewExhaustionTests.Third));

        await rig.ExecuteAsync(Settings());

        SeriesEvent(rig).EvidenceProblems.Should().ContainSingle().Which.Problem.Should().Contain("past this attempt's stop");
        rig.Set.Specs[2].Draft.Yaml.Should().NotContain(NewStep);
    }

    [Fact]
    public async Task SeriesContractRound_CutOnApprovedUnchangedMember_BecomesAFinding()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs())).Revisions(Edit);
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.BuildWithCuts, SeriesCut(First));
        using var _ = rig.RunContext.BeginScope(RunId);

        await rig.SeriesRound().RunAsync(rig.Run(), reviewed: [Second], CancellationToken.None);

        var round = SeriesEvent(rig);
        round.Cuts.Should().BeEmpty();
        round.Findings.Should().ContainSingle().Which.Text.Should().Contain($"{First} is the text a person approved");
        rig.Published.Should().BeEmpty("approved text is never re-cut at run time");
    }

    [Fact]
    public async Task SeriesContractRound_CallFails_StepLineSaysNotTaken()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs()));
        rig.OnSeriesReview = _ => SeriesContractReview.Failed("the reviewer call failed or its answer could not be read");

        var result = await rig.ExecuteAsync(Settings());

        result.IsSuccess.Should().BeTrue("the cleared prefix is still built");
        result.Message.Should().Contain("series contract round not taken: the reviewer call failed");
        rig.Events.Events.OfType<SpecReviewRoundEvent>().Should().NotContain(e => e.Scope == SpecReviewScopes.Series);
    }

    [Fact]
    public async Task SeriesContractRound_RunCapSpent_StepLineSaysNotTaken()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs()));
        var tracker = PipelineCostTracker.GetOrCreate(rig.Pipeline);
        tracker.ApplyCostCap(new CostCapValues { Usd = 10m, Tokens = 10_000_000 });
        tracker.SeedPriorSpend(new PriorSpendSnapshot(0, 0, 0, 0, 11m, 0, 0, 0, 0, 0m, string.Empty));

        var round = await rig.SeriesRound().RunAsync(rig.Run(), reviewed: [First, Second], CancellationToken.None);

        round.Line.Should().StartWith("series contract round not taken: ");
        rig.SeriesCalls.Should().BeEmpty("the share is waived, the run cap is not");
    }

    [Fact]
    public void SeriesContractRound_RunLook_AllowanceIsClearedMembersPlusTwelveNeverBelow24()
    {
        SeriesContractRound.Terms(2).Allowance.Should().Be(24);
        SeriesContractRound.Terms(12).Allowance.Should().Be(24);
        SeriesContractRound.Terms(20).Allowance.Should().Be(32);
        SeriesContractRound.Terms(20).EvidencePrefix.Should().Be(DerivationLookTerms.AttackReview.EvidencePrefix);
    }

    [Fact]
    public async Task ReviewSpecs_SpecAndSeriesRefusals_OneQuestionListingBoth()
    {
        var rig = new ReviewLoopRig(Contracted(TwoSpecs())).Answers(Refuse());
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.Refuse, findings: Finding);

        var result = await rig.ExecuteAsync(Settings());

        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        rig.Set.Handback!.Reason.Should().Contain(ReviewObjectionComment.MarkerFor(First))
            .And.Contain(ReviewObjectionComment.MarkerFor("series:" + First) + " (the series)");
        rig.Published.Count(p => p.Handback is not null).Should().Be(1, "one question lists both");
    }

    [Fact]
    public async Task ReviewSpecs_SeriesObjectionAlreadyAsked_ProceedsOnThePrefixMarker()
    {
        var rig = Thread(new ReviewLoopRig(Contracted(TwoSpecs())), SeriesAsked(First));
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.Refuse, findings: Finding);

        var result = await rig.ExecuteAsync(Settings());

        result.InsertNext.Should().BeNull();
        result.Message.Should().Contain($"series:{First}: its objection was asked before, built as reviewed");
    }

    [Fact]
    public async Task ReviewSpecs_SeriesObjectionOnALaterPrefix_AsksAgain()
    {
        var rig = Thread(new ReviewLoopRig(Contracted(ReviewExhaustionTests.ThreeSpecs()) with { Executed = [First] }),
            SeriesAsked(First));
        rig.OnSeriesReview = _ => Series(SpecReviewVerdicts.Refuse, findings: Finding);

        var result = await rig.ExecuteAsync(Settings());

        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        rig.Set.Handback!.Reason.Should().Contain(ReviewObjectionComment.MarkerFor("series:" + Second));
    }

    private static readonly SpecReviewFinding Finding = new("Both members mint event 87.", "shop/.agentsmith/specs/x.yaml:3 [A1]");

    private static SpecSet Contracted(SpecSet set) => set with { Contract = SeriesReviewFixtures.Contract(First) };

    private static SpecReviewCut SeriesCut(string specId) =>
        new(SpecRegisterNames.Steps, "Write the orders through the buffered", "Write the orders through the export queue",
            "shop/.agentsmith/specs/x.yaml:3 [A1]", specId);

    private static SeriesContractReview Series(string verdict, params SpecReviewCut[] cuts) =>
        new(new SpecAttackResult(verdict, cuts, []), []);

    private static SeriesContractReview Series(string verdict, SpecReviewFinding findings) =>
        new(new SpecAttackResult(verdict, [], [findings]), []);

    private static SpecReviewRoundEvent SeriesEvent(ReviewLoopRig rig) =>
        rig.Events.Events.OfType<SpecReviewRoundEvent>().Single(e => e.Scope == SpecReviewScopes.Series);

    private static ReviewLoopRig ShareSpentAfterTheFirst(SpecSet set)
    {
        var rig = new ReviewLoopRig(set);
        rig.OnReview = _ => rig.Pipeline.Set(ContextKeys.RunStartedAt, DateTimeOffset.UtcNow.AddSeconds(-1000));
        return rig;
    }

    private static async Task<IReadOnlyList<string?>> Built(ReviewLoopRig rig)
    {
        var spliced = await new SpecSequenceHandler(AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), TimeProvider.System, NullLogger<SpecSequenceHandler>.Instance)
            .ExecuteAsync(new SpecSequenceContext(rig.Pipeline), default);
        return [.. spliced.InsertNext!.Where(c => c.SpecId is not null).Select(c => c.SpecId).Distinct()];
    }

    private static TicketComment SeriesAsked(string head) =>
        ReviewExhaustionTests.Ours(ReviewObjectionQuestion.For([new StandingObjection(null, [Finding], head)]));

    private static ReviewLoopRig Thread(ReviewLoopRig rig, params TicketComment[] thread)
    {
        rig.Pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, thread);
        return rig;
    }
}
