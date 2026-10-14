using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Conversation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.Review.ReviewExhaustionTests;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: the review-objection question everywhere a hand-back is read — its comment and marker, the
/// pin that exists only for a question that was posted and left unanswered, the prompt that pin renders for the
/// deriver and for every reviewer, the manifest field, and the gate that still builds the specs before the park.
/// </summary>
public sealed class ReviewObjectionThreadTests
{
    private static readonly DateTimeOffset Later = new(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
    private static readonly SpecHandback Question = ReviewObjectionQuestion.For([Objection(First), Objection(Second)]);

    [Fact]
    public void Comment_ReviewObjection_HeadsWithItsOwnMarkerAndNamesEachSpec()
    {
        var body = SpecHandbackComment.Build(Question, null, "@author");

        body.Should().StartWith($"## Agent Smith — {ReviewObjectionComment.Marker}")
            .And.Contain(ReviewObjectionComment.MarkerFor(First)).And.Contain(ReviewObjectionComment.MarkerFor(Second))
            .And.Contain("(a) build the spec as reviewed").And.Contain("proceeds on (a)");
        body.Should().NotContain(SpecHandbackComment.QuestionMarker, "the ticket does not read two ways");
    }

    [Fact]
    public void Pin_ReviewObjectionUnanswered_IsFoundByItsOwnMarker()
    {
        var pipeline = Thread(OurObjection(First));

        var unanswered = Pin().Pin(Previous(), pipeline);

        unanswered.Should().NotBeNull();
        unanswered!.TakenLabel.Should().Be("(a)");
        pipeline.Get<string>(ContextKeys.SpecQuestionPin).Should().Contain("do not raise it again");
    }

    [Fact]
    public void Pin_ReviewObjectionNeverPosted_SetsNoPinAndPostsNoNotice()
    {
        foreach (var pipeline in new[] { Thread(), Thread(new TicketComment("author", Later, "any news?")) })
        {
            Pin().Pin(Previous(), pipeline).Should().BeNull("a question nobody saw was not left unanswered");
            pipeline.Has(ContextKeys.SpecQuestionPin).Should().BeFalse();
        }
    }

    [Fact]
    public void Pin_ReviewObjectionAnsweredOnThePullRequest_SetsNoPin()
    {
        var pipeline = Thread(OurObjection(First));
        var note = new PrReviewNote(new PrCommentAuthor("u", "r", "reviewer", "reviewer"), false, false, Later, "build it");
        pipeline.Set(ContextKeys.PrReviewFeedback, (IReadOnlyList<PrReviewFeedback>)[
            new PrReviewFeedback("shop", "https://github.com/o/shop/pull/4", [new PrReviewThread(null, null, null, [note])])]);

        Pin().Pin(Previous(), pipeline).Should().BeNull();
        pipeline.Has(ContextKeys.SpecQuestionPin).Should().BeFalse();
    }

    [Fact]
    public void OwnTicketComment_ReviewObjection_AwaitsAnAnswer() =>
        OwnTicketComment.AwaitsAnswer(OurObjection(First)).Should().BeTrue();

    [Fact]
    public void Conversation_ReviewObjectionHeading_IsFolded()
    {
        var comment = ConversationAssembly.Thread([OurObjection(First)]).Single();
        var handback = comment with
        {
            Id = "run:handback", Source = "run", Kind = RunConversationKinds.HandBack, At = comment.At.AddMinutes(-1),
        };

        ConversationAssembly.Folded([handback, comment]).Should().ContainSingle().Which.Id.Should().Be("run:handback");
    }

    [Fact]
    public void Manifest_QuestionKind_RoundTrips_AndAnOldManifestReadsReading()
    {
        var manifest = new SeriesManifest();
        var set = TwoSpecs() with { Handback = Question };

        var yaml = manifest.Serialize(set);
        manifest.HandbackOf(manifest.Parse(yaml)!)!.QuestionKind.Should().Be(SpecQuestionKind.ReviewObjection);

        var reading = new SpecHandback(SpecHandbackCase.Question, "reads two ways", Readings: ["a", "b"]);
        var old = manifest.Serialize(set with { Handback = reading });
        old.Should().NotContain("handback_question_kind", "a reading question writes what it wrote before");
        manifest.HandbackOf(manifest.Parse(old)!)!.QuestionKind.Should().Be(SpecQuestionKind.Reading);
    }

    [Fact]
    public void PinSection_ReviewObjection_SaysKeepTheSpecAndDoNotRaiseItAgain() =>
        UnansweredQuestionPromptSection.Render(new UnansweredQuestion(Question, "(a)", Question.TakenReading!))
            .Should().Contain("keep the spec as it stands, do not change it for this objection and do not raise it again")
            .And.Contain(ReviewObjectionComment.MarkerFor(First)).And.NotContain("reads two ways");

    [Fact]
    public async Task Reviewer_GivenThePin_ItsPromptCarriesIt()
    {
        var rig = new ReviewLoopRig();
        rig.Pipeline.Set(ContextKeys.SpecQuestionPin, "## A reviewer's objection the author did not answer");

        await rig.ExecuteAsync(Settings());

        rig.Inputs.Should().NotBeEmpty().And.OnlyContain(i => i.QuestionPin == rig.Pipeline.Get<string>(ContextKeys.SpecQuestionPin));
        SpecAttackPrompt.For(rig.Inputs[0], null).Should().Contain("## A reviewer's objection the author did not answer");
    }

    [Fact]
    public async Task SpecGate_ReviewObjectionSet_PublishesTheFirstSpec()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, TwoSpecs() with { Handback = Question });

        var result = await new SpecGateHandler(NullLogger<SpecGateHandler>.Instance).ExecuteAsync(
            new SpecGateContext(new Ticket(new TicketId("1"), "t", "d", null, "open", "azdo", []), pipeline), default);

        result.Message.Should().NotContain("nothing to gate");
        pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec).SpecId.Should().Be(First);
    }

    private static SpecSet Previous() => TwoSpecs() with { Handback = Question };

    private static UnansweredQuestionPin Pin() => new(NullLogger<UnansweredQuestionPin>.Instance);

    private static PipelineContext Thread(params TicketComment[] comments)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, comments);
        return pipeline;
    }
}
