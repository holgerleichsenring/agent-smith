using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
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
/// reasons would never match.
/// </summary>
public sealed class SpecHandbackTests
{
    private static SpecSetPointer Pointer(
        SpecHandbackCase last = SpecHandbackCase.None,
        string? sourceSha = null,
        int repeats = 0) =>
        new("azdo-1", "primary", "sha", 1, last, repeats, sourceSha);

    [Fact]
    public void Handback_SameCaseCodeAndNoSourceCommit_StopsHandingBack() =>
        SpecHandbackProgress.RepeatsWithoutProgress(
            Pointer(SpecHandbackCase.RequirementsContradictRepository, "sha"),
            SpecHandbackCase.RequirementsContradictRepository, "sha")
        .Should().BeTrue();

    [Fact]
    public void RepeatsWithoutProgress_SameCaseButSomethingWasCommitted_HandsBackAgain() =>
        SpecHandbackProgress.RepeatsWithoutProgress(
            Pointer(SpecHandbackCase.RequirementsContradictRepository, "sha"),
            SpecHandbackCase.RequirementsContradictRepository, "newer-sha")
        .Should().BeFalse();

    [Fact]
    public void RepeatsWithoutProgress_FirstHandbackEver_HandsBack() =>
        SpecHandbackProgress.RepeatsWithoutProgress(
            null, SpecHandbackCase.NotImplementable, "sha").Should().BeFalse();

    // The verdict comment carries NO question anchor, so no comment on the ticket can be
    // parsed as an answer — that is what makes "does not auto-retry on a comment" a
    // structural property rather than a rule someone has to remember.
    [Fact]
    public void Handback_NotImplementable_DoesNotAutoRetryOnComment()
    {
        var body = SpecHandbackComment.Build(
            new SpecHandback(SpecHandbackCase.NotImplementable, "the API does not exist"), null);

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
            "https://example.test/pr/1");

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

    [Fact]
    public void ParksOpenQuestions_Code_IsTrue_BecauseTheHandbackParks() =>
        PipelinePresets.ParksOpenQuestions(PipelinePresets.CodeName).Should().BeTrue(
            "the capability is derived from the command list, spliced block included");

    private static SpecHandbackHandler Handler(Mock<ITicketProvider> tickets)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(tickets.Object);
        return new SpecHandbackHandler(
            factory.Object,
            new SpecParkStatusResolver(new ClarificationParkStatusResolver()),
            new Application.Services.Persistence.InMemorySpecSetPointerStore(),
            NullLogger<SpecHandbackHandler>.Instance);
    }

    private static TrackerConnection Parkable() => new()
    {
        Type = TrackerType.AzureDevOps,
        NeedsClarificationStatus = "needs-info",
        NotImplementableStatus = "blocked",
    };

    private static SpecHandbackContext Context(PipelineContext pipeline, TrackerConnection tracker) =>
        new(new Ticket(new TicketId("1"), "t", "d", null, "open", "azdo", []), tracker, [], pipeline);

    private static PipelineContext PipelineWith(SpecHandback handback)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecHandback, handback);
        return pipeline;
    }
}
