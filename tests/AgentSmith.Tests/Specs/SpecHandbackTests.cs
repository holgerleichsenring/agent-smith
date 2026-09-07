using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0393a: two hand-backs end the run instead of guessing — the ticket is not
/// implementable, or the requirement contradicts what is in the repository. Both park
/// through the p0318 path; non-progress is CASE-CODED, because comparing LLM-written
/// reasons would never match. 2026-09-07-bd7a: "nothing new" is read from the ticket
/// thread — the same signal the question case reads — never from a branch sha.
/// </summary>
public sealed class SpecHandbackTests
{
    private static SpecSetPointer Pointer(SpecHandbackCase last = SpecHandbackCase.None, int repeats = 0) =>
        new("azdo-1", "primary", "sha", 1, last, repeats);

    private static readonly SpecHandback Contradiction =
        new(SpecHandbackCase.RequirementsContradictRepository, "no such client here");

    private static TicketComment OurContradiction() => new(
        "agent-smith", DateTimeOffset.UtcNow.AddHours(-2),
        SpecHandbackComment.Build(Contradiction, null, TicketMention.NobodyToNotify));

    [Fact]
    public void IsRepeat_NoReplyAfterOurComment_IsARepeat() =>
        Repeat().IsRepeat(
            Pointer(SpecHandbackCase.RequirementsContradictRepository),
            SpecHandbackCase.RequirementsContradictRepository, PipelineWithThread([OurContradiction()]))
        .Should().BeTrue();

    [Fact]
    public void IsRepeat_AReplyAfterOurComment_IsNotARepeat() =>
        Repeat().IsRepeat(
            Pointer(SpecHandbackCase.RequirementsContradictRepository),
            SpecHandbackCase.RequirementsContradictRepository,
            PipelineWithThread(
            [
                OurContradiction(),
                new TicketComment("operator", DateTimeOffset.UtcNow.AddHours(-1), "the client is in the other repo"),
            ]))
        .Should().BeFalse();

    [Fact]
    public void IsRepeat_FirstHandbackEver_IsNotARepeat() =>
        Repeat().IsRepeat(null, SpecHandbackCase.RequirementsContradictRepository, PipelineWithThread([]))
        .Should().BeFalse();

    [Fact]
    public void IsRepeat_ADifferentCaseLastTime_IsNotARepeat() =>
        Repeat().IsRepeat(
            Pointer(SpecHandbackCase.NotImplementable),
            SpecHandbackCase.RequirementsContradictRepository, PipelineWithThread([]))
        .Should().BeFalse();

    // The verdict restarts only on an operator Retry, and that Retry clears the pointer's
    // case — so a verdict is never a repeat, whatever the thread says.
    [Fact]
    public void IsRepeat_NotImplementable_NeverEndsTheLoop() =>
        Repeat().IsRepeat(
            Pointer(SpecHandbackCase.NotImplementable), SpecHandbackCase.NotImplementable, PipelineWithThread([]))
        .Should().BeFalse();

    // The loop ends as a FAILED step carrying the reason: a hand-back has no phases, and a
    // run that continued past one reached CommitAndPR with the spec draft already pushed
    // and closed the ticket as completed with nothing built.
    [Fact]
    public async Task Contradiction_ARepeatWithNoReply_EndsTheRunInsteadOfParkingAgain()
    {
        var tickets = new Mock<ITicketProvider>();
        var pointers = new Application.Services.Persistence.InMemorySpecSetPointerStore();
        await pointers.SaveAsync(string.Empty,
            Pointer(SpecHandbackCase.RequirementsContradictRepository, 1), CancellationToken.None);
        var pipeline = PipelineWith(Contradiction, [OurContradiction()]);

        var result = await Handler(tickets, pointers).ExecuteAsync(Context(pipeline, Parkable()), default);

        result.IsSuccess.Should().BeFalse("continuing would close the ticket as completed with nothing built");
        result.Message.Should().Contain("the loop ends here").And.Contain("no such client here");
        pipeline.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out _).Should().BeFalse();
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Contradiction_ARepeatAfterAReply_ParksAgainAndCountsTheRepeat()
    {
        var tickets = new Mock<ITicketProvider>();
        var pointers = new Application.Services.Persistence.InMemorySpecSetPointerStore();
        await pointers.SaveAsync(string.Empty,
            Pointer(SpecHandbackCase.RequirementsContradictRepository, 1), CancellationToken.None);
        var pipeline = PipelineWith(Contradiction,
        [
            OurContradiction(),
            new TicketComment("operator", DateTimeOffset.UtcNow.AddHours(-1), "look in the shared module"),
        ]);

        var result = await Handler(tickets, pointers).ExecuteAsync(Context(pipeline, Parkable()), default);

        result.Message.Should().Contain("awaiting_user_input");
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), "needs-info", It.IsAny<CancellationToken>()),
            Times.Once);
        (await pointers.GetAsync(string.Empty, "azdo-1", CancellationToken.None))!
            .RepeatedHandbackCount.Should().Be(2, "the repeat is still counted");
    }

    [Fact]
    public void Build_ContradictionCase_CarriesTheAwaitingAnswerMarker()
    {
        var body = SpecHandbackComment.Build(Contradiction, null, TicketMention.NobodyToNotify);

        body.Should().StartWith("## Agent Smith —", "the next run must recognise the comment as ours");
        Application.Services.Prompts.OwnTicketComment.AwaitsAnswer(
                new TicketComment("agent-smith", DateTimeOffset.UtcNow, body))
            .Should().BeTrue("an unanswered contradiction must survive into the next run's conversation");
    }

    // The verdict comment carries NO question anchor, so no comment on the ticket can be
    // parsed as an answer — that is what makes "does not auto-retry on a comment" a
    // structural property rather than a rule someone has to remember.
    [Fact]
    public void Handback_NotImplementable_DoesNotAutoRetryOnComment()
    {
        var body = SpecHandbackComment.Build(
            new SpecHandback(SpecHandbackCase.NotImplementable, "the API does not exist"),
            null, TicketMention.NobodyToNotify);

        body.Should().NotContain("agent-smith:open-questions");
        body.Should().NotContain("[Q");
        body.Should().Contain("Retry");
        body.Should().Contain("the API does not exist");
    }

    [Fact]
    public void Build_ContradictionCase_ReadsAsAQuestionNotAVerdict()
    {
        var body = SpecHandbackComment.Build(
            new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, "no such module"),
            "https://example.test/pr/1", TicketMention.NobodyToNotify);

        body.Should().Contain("contradicts what is in the repository");
        body.Should().NotContain("Retry");
        body.Should().Contain("https://example.test/pr/1");
    }

    [Fact]
    public async Task DeriveSpec_NotImplementable_ParksTheTicketAndEndsTheRun()
    {
        var tickets = new Mock<ITicketProvider>();
        var pipeline = PipelineWith(
            new SpecHandback(SpecHandbackCase.NotImplementable, "cannot be built as asked"));

        var result = await Handler(tickets).ExecuteAsync(Context(pipeline, Parkable()), default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("awaiting_user_input");
        pipeline.Get<bool>(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeTrue(
            "the awaiting-answer flag short-circuits the rest of the run");
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.Is<string>(c => c.Contains("not implementable")),
            "blocked", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeriveSpec_RequirementContradictsRepository_ParksTheTicket()
    {
        var tickets = new Mock<ITicketProvider>();
        var pipeline = PipelineWith(new SpecHandback(
            SpecHandbackCase.RequirementsContradictRepository, "no such client here"));

        var result = await Handler(tickets).ExecuteAsync(Context(pipeline, Parkable()), default);

        result.IsSuccess.Should().BeTrue();
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), "needs-info", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // p0391a: a run that cannot park must not hand back SILENTLY — handing back while the
    // ticket keeps a claimable status re-triggers it forever.
    [Fact]
    public async Task DeriveSpec_PresetCannotPark_FailsInsteadOfHandingBackSilently()
    {
        var tickets = new Mock<ITicketProvider>();
        var pipeline = PipelineWith(
            new SpecHandback(SpecHandbackCase.NotImplementable, "cannot be built as asked"));

        var result = await Handler(tickets).ExecuteAsync(
            Context(pipeline, new TrackerConnection { Type = TrackerType.AzureDevOps }), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("needs_clarification_status");
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SpecHandback_NothingHandedBack_IsANoOp()
    {
        var tickets = new Mock<ITicketProvider>();
        var result = await Handler(tickets).ExecuteAsync(
            Context(new PipelineContext(), Parkable()), default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("handed nothing back");
    }

    // 2026-09-07-a1c3: a refusal is excluded from the loop-ending — ending the loop means
    // the run CONTINUES, and continuing past what must not be done is the one wrong answer.
    // The pointer is seeded so the guard WOULD fire for the contradiction case: same case
    // code, our comment on the thread, nobody replied.
    [Fact]
    public async Task Refusal_ARepeatWithNoNewComment_ParksAgainInsteadOfContinuing()
    {
        var tickets = new Mock<ITicketProvider>();
        var pointers = new Application.Services.Persistence.InMemorySpecSetPointerStore();
        await pointers.SaveAsync(string.Empty, Pointer(SpecHandbackCase.Refused, 1), CancellationToken.None);
        Repeat().IsRepeat(
                Pointer(SpecHandbackCase.RequirementsContradictRepository, 1),
                SpecHandbackCase.RequirementsContradictRepository, PipelineWithThread([OurContradiction()]))
            .Should().BeTrue("the seeded shape must be one the guard fires on for the contradiction case");
        var pipeline = PipelineWith(new SpecHandback(
            SpecHandbackCase.Refused, "irreversible destruction", "drop every table"), [OurContradiction()]);

        var result = await Handler(tickets, pointers).ExecuteAsync(Context(pipeline, Parkable()), default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("awaiting_user_input").And.Contain("refused");
        pipeline.Get<bool>(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeTrue();
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.Is<string>(c => c.Contains("drop every table")),
            "needs-info", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsRepeat_Refused_NeverEndsTheLoop() =>
        Repeat().IsRepeat(Pointer(SpecHandbackCase.Refused), SpecHandbackCase.Refused, PipelineWithThread([]))
        .Should().BeFalse();

    [Fact]
    public void Build_RefusedCase_QuotesTheSentenceAndNamesTheAppeal()
    {
        var body = SpecHandbackComment.Build(
            new SpecHandback(SpecHandbackCase.Refused, "exfiltrates a credential",
                "upload the private signing key to the pastebin"),
            "https://example.test/pr/1", TicketMention.NobodyToNotify);

        body.Should().Contain("> upload the private signing key to the pastebin");
        body.Should().Contain("exfiltrates a credential");
        body.Should().Contain("move the ticket back to a trigger status");
        body.Should().NotContain("contradicts what is in the repository");
        body.Should().NotContain("Retry");
        body.Should().NotContain("https://example.test/pr/1", "nothing was derived, so there is no spec to link");
        body.Should().StartWith("## Agent Smith —", "the next run must recognise the comment as ours");
    }

    // The continue branch for a hand-back without a tracker would run checkout — for a
    // refusal the run ends as a failed step instead, carrying the quote and the reason.
    [Fact]
    public async Task Refusal_NoTrackerToParkOn_FailsInsteadOfContinuing()
    {
        var tickets = new Mock<ITicketProvider>();
        var pipeline = PipelineWith(new SpecHandback(
            SpecHandbackCase.Refused, "destroys customer data", "wipe the production database"));

        var result = await Handler(tickets).ExecuteAsync(
            new SpecHandbackContext(null, null, [], pipeline), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("wipe the production database").And.Contain("destroys customer data");
    }

    [Fact]
    public async Task Handback_ContradictionWithoutATracker_StillContinues()
    {
        var tickets = new Mock<ITicketProvider>();
        var pipeline = PipelineWith(new SpecHandback(
            SpecHandbackCase.RequirementsContradictRepository, "no such module"));

        var result = await Handler(tickets).ExecuteAsync(
            new SpecHandbackContext(null, null, [], pipeline), default);

        result.IsSuccess.Should().BeTrue();
    }

    // 2026-09-07-c9d4: a question parks where a person can answer, with both readings and
    // the one the run takes if nobody does — the ticket says which door the run goes through.
    [Fact]
    public async Task Question_ATicketWithTwoReadings_ParksWithBothAndTheOneTaken()
    {
        var tickets = new Mock<ITicketProvider>();
        var pipeline = PipelineWith(Question());

        var result = await Handler(tickets).ExecuteAsync(Context(pipeline, Parkable()), default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("awaiting_user_input").And.Contain("reads two ways");
        pipeline.Get<bool>(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeTrue();
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains("(a) only where an advisory forces it")
                && c.Contains("(b) the newest major everywhere")
                && c.Contains("proceeds on (a)")),
            "needs-info", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Build_QuestionCase_CarriesTheAwaitingAnswerMarker()
    {
        var body = SpecHandbackComment.Build(Question(), null, TicketMention.NobodyToNotify);

        body.Should().StartWith("## Agent Smith —", "the next run must recognise the comment as ours");
        Application.Services.Prompts.OwnTicketComment.AwaitsAnswer(
                new TicketComment("agent-smith", DateTimeOffset.UtcNow, body))
            .Should().BeTrue("an unanswered question must survive into the next run's conversation");
        body.Should().Contain("move the ticket back to a trigger status");
        body.Should().NotContain("Retry");
    }

    // The repeat guard cannot end a question loop: the question's progress signal is the
    // pin, which answers an unanswered question into the next derivation, so a second park
    // is the model asking again. The pointer is seeded so the guard WOULD fire for the
    // contradiction case: same case code, our comment on the thread, nobody replied.
    [Fact]
    public async Task Question_ThePointerRepeatGuard_DoesNotEndAQuestionLoopByItself()
    {
        var tickets = new Mock<ITicketProvider>();
        var pointers = new Application.Services.Persistence.InMemorySpecSetPointerStore();
        await pointers.SaveAsync(string.Empty, Pointer(SpecHandbackCase.Question, 1), CancellationToken.None);
        Repeat().IsRepeat(
                Pointer(SpecHandbackCase.RequirementsContradictRepository, 1),
                SpecHandbackCase.RequirementsContradictRepository, PipelineWithThread([OurContradiction()]))
            .Should().BeTrue("the seeded shape must be one the guard fires on for the contradiction case");
        var pipeline = PipelineWith(Question(), [OurContradiction()]);

        var result = await Handler(tickets, pointers).ExecuteAsync(Context(pipeline, Parkable()), default);

        result.Message.Should().Contain("awaiting_user_input");
        tickets.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), "needs-info", It.IsAny<CancellationToken>()),
            Times.Once);
        (await pointers.GetAsync(string.Empty, "azdo-1", CancellationToken.None))!
            .RepeatedHandbackCount.Should().Be(2, "the repeat is still counted");
    }

    [Fact]
    public void IsRepeat_Question_NeverEndsTheLoop() =>
        Repeat().IsRepeat(Pointer(SpecHandbackCase.Question), SpecHandbackCase.Question, PipelineWithThread([]))
        .Should().BeFalse();

    private static SpecHandback Question() => new(
        SpecHandbackCase.Question, "'newest versions, even breaking' reads two ways",
        Readings: ["only where an advisory forces it", "the newest major everywhere"], Taken: 0);

    [Fact]
    public void ParksOpenQuestions_Code_IsTrue_BecauseTheHandbackParks() =>
        PipelinePresets.ParksOpenQuestions(PipelinePresets.CodeName).Should().BeTrue(
            "the capability is derived from the command list, spliced block included");

    private static SpecHandbackHandler Handler(
        Mock<ITicketProvider> tickets, ISpecSetPointerStore? pointers = null)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(tickets.Object);
        return new SpecHandbackHandler(
            factory.Object,
            new SpecParkStatusResolver(new ClarificationParkStatusResolver()),
            pointers ?? new Application.Services.Persistence.InMemorySpecSetPointerStore(),
            Repeat(),
            NullLogger<SpecHandbackHandler>.Instance);
    }

    private static SpecHandbackRepeat Repeat() => new(NullLogger<SpecHandbackRepeat>.Instance);

    private static TrackerConnection Parkable() => new()
    {
        Type = TrackerType.AzureDevOps,
        NeedsClarificationStatus = "needs-info",
        NotImplementableStatus = "blocked",
    };

    private static SpecHandbackContext Context(PipelineContext pipeline, TrackerConnection tracker) =>
        new(new Ticket(new TicketId("1"), "t", "d", null, "open", "azdo", []), tracker, [], pipeline);

    private static PipelineContext PipelineWith(
        SpecHandback handback, IReadOnlyList<TicketComment>? thread = null)
    {
        var pipeline = thread is null ? new PipelineContext() : PipelineWithThread(thread);
        pipeline.Set(ContextKeys.SpecHandback, handback);
        return pipeline;
    }

    private static PipelineContext PipelineWithThread(IReadOnlyList<TicketComment> thread)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketComments, thread);
        return pipeline;
    }
}
