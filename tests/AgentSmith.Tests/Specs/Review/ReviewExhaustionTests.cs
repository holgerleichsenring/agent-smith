using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: a REFUSE standing when the review ran out is asked once — read from the thread per spec
/// (or per series prefix), never past the budget's stop, never for cuts that could not be applied — and the
/// ticket parks at the earliest asked spec after the specs before it are built.
/// </summary>
public sealed class ReviewExhaustionTests
{
    public const string Third = "2026-10-14-5678c";
    private static readonly DateTimeOffset Asked = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly SpecReviewFinding Finding = new("The cited handler does not exist.", "shop/src/Handler.cs:12 [A1]");

    [Fact]
    public void Exhaustion_StandingRefuse_NoObjectionComment_Asks() =>
        Decide([Objection(First)]).Should().ContainSingle().Which.SpecId.Should().Be(First);

    [Fact]
    public void Exhaustion_UnansweredObjectionCommentForThisSpec_Proceeds() =>
        Decide([Objection(First)], OurObjection(First)).Should().BeEmpty();

    [Fact]
    public void Exhaustion_ObjectionCommentStoredHtmlEscaped_StillReadsAsAsked()
    {
        var ours = OurObjection(First);

        Decide([Objection(First)], ours with { Body = System.Net.WebUtility.HtmlEncode(ours.Body) }).Should().BeEmpty("a tracker that escapes the comment must not ask again");
    }

    [Fact]
    public void Markers_CarryNoCharacterAnHtmlTrackerEscapes()
    {
        foreach (var marker in new[] { ReviewObjectionComment.Marker, ReviewObjectionComment.MarkerFor("series:" + First) })
            System.Net.WebUtility.HtmlEncode(marker).Should().Be(marker);
    }

    [Fact]
    public void Exhaustion_ObjectionCommentAnswered_Proceeds() =>
        Decide([Objection(First)], OurObjection(First), new TicketComment("author", Asked.AddHours(1), "build it"))
            .Should().BeEmpty("an asked objection proceeds; the answer is the next derivation's input");

    [Fact]
    public void Exhaustion_ReadingQuestionPin_StillAsksTheObjection()
    {
        var rig = new ReviewLoopRig();
        rig.Pipeline.Set(ContextKeys.SpecQuestionPin, "## The question from the last run was left unanswered");

        Exhaustion().Decide([Objection(First)], rig.Pipeline).Should().ContainSingle("the pin is not the basis");
    }

    [Fact]
    public async Task Exhaustion_LaterAttemptWithoutPin_UnansweredObjection_DoesNotParkAgain()
    {
        var rig = Thread(new ReviewLoopRig().Answers(Refuse()), OurObjection(First));

        var result = await rig.ExecuteAsync(Settings());

        result.InsertNext.Should().BeNull();
        rig.Pipeline.Has(ContextKeys.SpecSequenceStop).Should().BeFalse();
        rig.Set.Handback.Should().BeNull();
        result.Message.Should().Contain($"{First}: its objection was asked before, built as reviewed");
    }

    [Fact]
    public void Exhaustion_ObjectionOnAnotherSpec_AsksOnce() =>
        Decide([Objection(First), Objection(Second)], OurObjection(First))
            .Should().ContainSingle().Which.SpecId.Should().Be(Second);

    [Fact]
    public void Exhaustion_UnionOfSpecAndSeriesObjections_OneQuestionListingEach()
    {
        var ask = Decide([Objection(Second), new StandingObjection(null, [Finding], First)]);

        ask.Should().HaveCount(2);
        var reason = ReviewObjectionQuestion.For(ask).Reason;
        reason.Should().Contain(ReviewObjectionComment.MarkerFor(Second))
            .And.Contain(ReviewObjectionComment.MarkerFor("series:" + First) + " (the series)");
    }

    [Fact]
    public async Task ReviewSpecs_SeriesObjection_ParksBeforeAnySpecIsBuilt()
    {
        var rig = new ReviewLoopRig();

        var result = await Park(rig).ApplyAsync(rig.Pipeline, [new StandingObjection(null, [Finding], First)],
            AgentSmith.Domain.Models.CommandResult.Ok("reviewed"), default);

        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        rig.Set.Handback!.QuestionKind.Should().Be(SpecQuestionKind.ReviewObjection);
    }

    [Fact]
    public void ReviewSpecs_SeriesObjectionOnALaterPrefix_AsksAgain() =>
        Decide([new StandingObjection(null, [Finding], Second)], OurSeriesObjection(First))
            .Should().ContainSingle("a later prefix is a new objection");

    [Fact]
    public void Exhaustion_ResiduesOnly_NeverAsks() =>
        StandingObjection.Of(Outcome(SpecReviewVerdicts.BuildWithCuts) with
        {
            Residues = [new SpecReviewResidue(SpecRegisterNames.Goal, "one dated file")],
        }).Should().BeNull();

    [Fact]
    public void Exhaustion_BudgetStoppedSpec_IsNotAsked()
    {
        StandingObjection.Of(Outcome(SpecReviewVerdicts.Refuse) with { BudgetStop = "share spent" }).Should().BeNull();
        StandingObjection.Of(Outcome(SpecReviewVerdicts.Refuse)).Should().NotBeNull();
    }

    [Fact]
    public void Exhaustion_ObjectionAfterTheBudgetStop_DoesNotMoveTheStop()
    {
        var rig = new ReviewLoopRig(ThreeSpecs());
        rig.Pipeline.Set(ContextKeys.SpecSequenceStop, new SpecSequenceStop(Second, "share spent", Park: false));

        Exhaustion().Decide([Objection(Second), Objection(Third)], rig.Pipeline).Should().BeEmpty();
        rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop).Should().Be(
            new SpecSequenceStop(Second, "share spent", Park: false));
    }

    [Fact]
    public void Question_Reason_QuotesTheEvidenceLine() =>
        ReviewObjectionQuestion.For([Objection(First)]).Reason
            .Should().Contain(ReviewObjectionComment.MarkerFor(First))
            .And.Contain("- The cited handler does not exist. — shop/src/Handler.cs:12 [A1]");

    [Fact]
    public void Question_TakenReading_IsBuildTheSpecAsReviewed()
    {
        var question = ReviewObjectionQuestion.For([Objection(First)]);

        question.Case.Should().Be(SpecHandbackCase.Question);
        question.TakenReading.Should().Be("build the spec as reviewed; the objection stays on record");
        question.Readings.Should().HaveCount(2);
    }

    [Fact]
    public async Task Question_KeepsTheReviewedSpecsAndTheExecutedHead()
    {
        var rig = new ReviewLoopRig(TwoSpecs() with { Executed = [First] }).Answers(Refuse());

        var result = await rig.ExecuteAsync(Settings());

        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        rig.Set.Specs.Should().HaveCount(2);
        rig.Set.Executed.Should().Equal(First);
        rig.Set.Handback!.QuestionKind.Should().Be(SpecQuestionKind.ReviewObjection);
        rig.Set.Current.Cause.Should().Be(ReviewObjectionPark.Cause);
    }

    [Fact]
    public async Task ReviewSpecs_RefuseOnFirstTailSpec_SplicesSpecHandbackAndParks()
    {
        var rig = new ReviewLoopRig().Answers(Refuse());

        var result = await rig.ExecuteAsync(Settings());

        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        result.Message.Should().Contain("parked before any spec is built");
        rig.Pipeline.Has(ContextKeys.SpecSequenceStop).Should().BeFalse();
    }

    [Fact]
    public async Task ReviewSpecs_RefuseOnThirdSpec_BuildsTheFirstTwoThenParksAtTheThird()
    {
        var rig = new ReviewLoopRig(ThreeSpecs()).Answers(Build(), Build(), Refuse());

        var result = await rig.ExecuteAsync(Settings(share: 100));

        result.InsertNext.Should().BeNull();
        rig.Pipeline.Get<SpecSequenceStop>(ContextKeys.SpecSequenceStop).Should().Match<SpecSequenceStop>(
            s => s.BeforeSpecId == Third && s.Park);
        var spliced = await new SpecSequenceHandler(AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), TimeProvider.System, NullLogger<SpecSequenceHandler>.Instance)
            .ExecuteAsync(new SpecSequenceContext(rig.Pipeline), default);
        spliced.InsertNext!.Where(c => c.SpecId is not null).Select(c => c.SpecId).Distinct()
            .Should().Equal(First, Second);
        spliced.InsertNext![^1].Name.Should().Be(CommandNames.SpecHandback);
    }

    internal static StandingObjection Objection(string specId) => new(specId, [Finding]);

    internal static TicketComment OurObjection(string specId) => Ours(ReviewObjectionQuestion.For([Objection(specId)]));

    internal static TicketComment Ours(SpecHandback handback) =>
        new("agent-smith", Asked, SpecHandbackComment.Build(handback, null, string.Empty));

    internal static SpecSet ThreeSpecs()
    {
        var two = TwoSpecs();
        var third = CutRevisionFixtures.With($"spec: {Second}", $"spec: {Third}", SecondYaml());
        return two with { Specs = [.. two.Specs, Spec(CutRevisionFixtures.Draft(third) with { Repos = ["shop"] })] };
    }

    private static TicketComment OurSeriesObjection(string head) =>
        Ours(ReviewObjectionQuestion.For([new StandingObjection(null, [Finding], head)]));

    private static IReadOnlyList<StandingObjection> Decide(
        IReadOnlyList<StandingObjection> objections, params TicketComment[] thread) =>
        Exhaustion().Decide(objections, Thread(new ReviewLoopRig(ThreeSpecs()), thread).Pipeline);

    private static ReviewLoopRig Thread(ReviewLoopRig rig, params TicketComment[] thread)
    {
        rig.Pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, thread);
        return rig;
    }

    private static ReviewExhaustion Exhaustion() => new(NullLogger<ReviewExhaustion>.Instance);

    private static ReviewObjectionPark Park(ReviewLoopRig rig) =>
        new(new SpecReviewRevision(rig.Publisher.Object), NullLogger<ReviewObjectionPark>.Instance);

    private static SpecReviewOutcome Outcome(string verdict) =>
        new(CutRevisionFixtures.Draft(), 4, verdict, [Finding], []);
}
