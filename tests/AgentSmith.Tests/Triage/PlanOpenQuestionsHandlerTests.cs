using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
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
        var handler = new PlanOpenQuestionsHandler(poster.Object, NullLogger<PlanOpenQuestionsHandler>.Instance);

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
        var handler = new PlanOpenQuestionsHandler(poster.Object, NullLogger<PlanOpenQuestionsHandler>.Instance);

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
            ticketConfig, ticket.Id, plan.OpenQuestions, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoPlanInContext_NoOp()
    {
        var poster = new Mock<IPlanOpenQuestionsPoster>();
        var handler = new PlanOpenQuestionsHandler(poster.Object, NullLogger<PlanOpenQuestionsHandler>.Instance);

        var context = new PlanOpenQuestionsContext(NewTicket(), NewTicketConfig(), new PipelineContext());

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        poster.VerifyNoOtherCalls();
    }

    private static Plan NewPlan(PlanStatus status, IReadOnlyList<PlanOpenQuestion>? questions = null)
        => new("Summary", Array.Empty<PlanStep>(), "{}")
        {
            Status = status,
            OpenQuestions = questions ?? Array.Empty<PlanOpenQuestion>()
        };

    private static Ticket NewTicket()
        => new(new TicketId("42"), "Add caching", "desc", null, "Open", "github");

    private static TicketConfig NewTicketConfig()
        => new() { Type = "github" };
}
