using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Triage;

public sealed class PlanOpenQuestionsHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_StatusComplete_NoComment()
    {
        var poster = new Mock<IPlanOpenQuestionsPoster>();
        var handler = NewHandler(poster);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, NewPlan(PlanStatus.Complete));
        var context = new PlanOpenQuestionsContext(NewTicket(), NewTicketConfig(), pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        poster.VerifyNoOtherCalls();
        pipeline.Has(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_StatusNeedsUserInput_PostsAndParks()
    {
        var poster = new Mock<IPlanOpenQuestionsPoster>();
        var handler = NewHandler(poster);

        var plan = NewPlan(PlanStatus.NeedsUserInput, new[]
        {
            new PlanOpenQuestion("1", "?", Array.Empty<string>())
        });
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, plan);
        var ticketConfig = NewTicketConfig();
        var ticket = NewTicket();
        var context = new PlanOpenQuestionsContext(ticket, ticketConfig, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("awaiting_user_input");
        pipeline.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out var awaiting).Should().BeTrue();
        awaiting.Should().BeTrue();
        poster.Verify(p => p.PostAsync(
            ticketConfig, ticket.Id, plan.OpenQuestions, "Question", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClarificationGate_EmptyDescription_HaltsPostsOpenQuestionsNoPr()
    {
        // p0318: a title-only ticket (empty effective body) halts even when no plan or a
        // Complete plan is present — the gate synthesizes a clarification question, posts
        // it, and sets the awaiting flag so the executor stops before AgenticMaster/CommitAndPR.
        var poster = new Mock<IPlanOpenQuestionsPoster>();
        var handler = NewHandler(poster);

        var pipeline = new PipelineContext();   // no Plan; the park status comes from the tracker
        var ticket = new Ticket(new TicketId("18969"), "Blank page on first load", "", null, "Active", "azuredevops");
        var context = new PlanOpenQuestionsContext(ticket, NewTicketConfig(), pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("awaiting_user_input");
        pipeline.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out var awaiting).Should().BeTrue();
        awaiting.Should().BeTrue();
        // exactly one synthesized question, parked in the tracker's configured status.
        poster.Verify(p => p.PostAsync(
            It.IsAny<TrackerConnection>(), ticket.Id,
            It.Is<IReadOnlyList<PlanOpenQuestion>>(q => q.Count == 1), "Question", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClarificationGate_OpenQuestionsPresent_MarksNeedsClarification()
    {
        // p0318: with needs_clarification_status configured, the gate parks the ticket in
        // that native status (passed to the poster) so discovery does not re-claim it.
        var poster = new Mock<IPlanOpenQuestionsPoster>();
        var handler = NewHandler(poster);

        var plan = NewPlan(PlanStatus.NeedsUserInput, new[]
        {
            new PlanOpenQuestion("1", "Which cache backend?", Array.Empty<string>())
        });
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, plan);
        pipeline.Set(ContextKeys.NeedsClarificationStatus, "Question");
        var ticket = NewTicket();
        var context = new PlanOpenQuestionsContext(ticket, NewTicketConfig(), pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Question");
        poster.Verify(p => p.PostAsync(
            It.IsAny<TrackerConnection>(), ticket.Id, plan.OpenQuestions, "Question", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClarificationGate_ParkStatusUnset_FailsLoudInsteadOfSkippingThePark()
    {
        // p0391: an unset needs_clarification_status used to log "(not parked)" and end the run
        // Ok — the ticket kept a trigger status, discovery re-claimed it, and the same run
        // repeated. It is a configuration error now; the load-time validator normally makes it
        // unreachable, and reaching it must never look like a successful park.
        var poster = new Mock<IPlanOpenQuestionsPoster>();
        var handler = NewHandler(poster);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, NewPlan(PlanStatus.NeedsUserInput));
        var context = new PlanOpenQuestionsContext(
            NewTicket(), NewTicketConfig(parkStatus: null), pipeline);

        var act = () => handler.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*needs_clarification_status*");
        poster.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_NoPlanInContext_NoOp()
    {
        var poster = new Mock<IPlanOpenQuestionsPoster>();
        var handler = NewHandler(poster);

        var context = new PlanOpenQuestionsContext(NewTicket(), NewTicketConfig(), new PipelineContext());

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        poster.VerifyNoOtherCalls();
    }

    // p0391: the park status is resolved (trigger seed, then tracker base) and an unset one
    // is a configuration error — the handler no longer degrades to a "(not parked)" log line.
    private static PlanOpenQuestionsHandler NewHandler(Mock<IPlanOpenQuestionsPoster> poster) =>
        new(poster.Object, new ClarificationParkStatusResolver(),
            NullLogger<PlanOpenQuestionsHandler>.Instance);

    private static Plan NewPlan(PlanStatus status, IReadOnlyList<PlanOpenQuestion>? questions = null)
        => new("Summary", Array.Empty<PlanStep>(), "{}")
        {
            Status = status,
            OpenQuestions = questions ?? Array.Empty<PlanOpenQuestion>()
        };

    private static Ticket NewTicket()
        => new(new TicketId("42"), "Add caching", "desc", null, "Open", "github");

    private static TrackerConnection NewTicketConfig(string? parkStatus = "Question")
        => new() { Type = TrackerType.GitHub, NeedsClarificationStatus = parkStatus };
}
