using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.Review.ReviewExhaustionTests;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: the next run over a branch set carrying a review-objection question, through the source
/// resolver. The hand-back never survives the read; a bare re-trigger over an asked, unanswered objection builds
/// the reviewed set unchanged; an answer, or an objection never posted, follows the amendment rule.
/// </summary>
public sealed class ReviewObjectionSourceTests
{
    private const string Key = "shop-42";
    private static readonly DateTimeOffset After = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly SpecSourceResolver _resolver = new(
        new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), TestSupport.ApprovedSetDoubles.Continuation(),
        new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance), NullLogger<SpecSourceResolver>.Instance);

    [Fact]
    public void Source_UnansweredReviewObjection_UsesTheReviewedSetWithoutHandback()
    {
        var decision = Decide(Asked(), Thread(OurObjection(First)));

        decision.Cause.Should().Be(SpecRevisionCause.Retrigger);
        decision.NeedsModel.Should().BeFalse("nothing is executed, yet the reviewed set is built as it stands");
        decision.Set!.Handback.Should().BeNull();
        decision.Set.Specs.Should().HaveCount(2);
    }

    [Fact]
    public void Source_AnsweredReviewObjection_DerivesOverTheRenderedPreviousCut()
    {
        var decision = Decide(Asked() with { Executed = [First] },
            Thread(OurObjection(First), new TicketComment("author", After, "change it as the reviewer asks")));

        decision.Cause.Should().Be(SpecRevisionCause.Comment);
        decision.NeedsModel.Should().BeTrue();
        PreviousCutPromptSection.Render(decision.Set, decision.Cause!)
            .Should().Contain($"{First} (EXECUTED — keep exactly as it is)").And.Contain($"{Second} (not started)");
    }

    [Fact]
    public void Source_AnsweredReviewObjectionWithoutDerivation_DoesNotParkAgain()
    {
        var pipeline = Thread(OurObjection(First));
        var note = new PrReviewNote(new PrCommentAuthor("u", "r", "reviewer", "reviewer"), false, false, After, "fine");
        pipeline.Set(ContextKeys.PrReviewFeedback, (IReadOnlyList<PrReviewFeedback>)[
            new PrReviewFeedback("shop", "https://github.com/o/shop/pull/4", [new PrReviewThread(null, null, null, [note])])]);

        var decision = Decide(Asked() with { Executed = [First] }, pipeline);

        decision.NeedsModel.Should().BeFalse();
        decision.Set!.IsHandedBack.Should().BeFalse("a set used unchanged would otherwise be republished and park again");
    }

    [Fact]
    public void Source_TicketEditedAfterUnansweredObjection_FollowsTheAmendmentRule()
    {
        var decision = Decide(Asked() with { TicketFingerprint = "an older text" }, Thread(OurObjection(First)));

        decision.Cause.Should().Be(SpecRevisionCause.TicketEdit);
        decision.NeedsModel.Should().BeTrue();
        decision.Set!.Handback.Should().BeNull();
    }

    [Fact]
    public void Source_ReviewObjectionNeverPosted_StripsTheHandbackAndFollowsTheAmendmentRule()
    {
        var decision = Decide(Asked(), Thread());

        decision.Cause.Should().Be(SpecRevisionCause.Retrigger);
        decision.NeedsModel.Should().BeTrue("an unexecuted set is re-derived on a bare re-trigger, as today");
        decision.Set!.Handback.Should().BeNull();
    }

    private SpecSourceResolver.Decision Decide(SpecSet set, PipelineContext pipeline) =>
        _resolver.Decide(SpecSetOnBranch.Answered(new SpecSetReadResult(set, "sha-1")), Ticket(),
            new SpecSetPointer(Key, "shop", "sha-1", 1), pipeline, Key, TicketLabelVocabulary.Default);

    private static SpecSet Asked() => TwoSpecs() with
    {
        Handback = ReviewObjectionQuestion.For([Objection(First)]),
        TicketFingerprint = TicketTextFingerprint.Of(Ticket()),
    };

    private static PipelineContext Thread(params TicketComment[] comments)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, comments);
        return pipeline;
    }

    private static Ticket Ticket() =>
        new(new TicketId("42"), "Export the orders", "Export every order of the day.", null, "open", "azdo", []);
}
