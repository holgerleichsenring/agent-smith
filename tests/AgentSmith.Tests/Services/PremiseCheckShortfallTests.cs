using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-17-0e79c: the hand-back is a FAILING STEP, which is also how the work survives. A
/// failing step runs the finalizer tail, so the phases that already verified reach their pull
/// request as a shortfall instead of dying with the run.
/// </summary>
public sealed class PremiseCheckShortfallTests
{
    [Fact]
    public async Task PremiseCheck_FalsePremise_EarlierVerifiedPhasesStillDeliver()
    {
        var h = new PipelineExecutorTestBuilder();
        var ticket = new Mock<ITicketProvider>();
        h.TicketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticket.Object);
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        Arrange(h, CommandNames.CheckPhasePremises, project, pipeline,
            CommandResult.Fail("False premise in p0001b: \"the bus is a singleton\" — [M1] …"));
        var commitAndPr = Arrange(h, CommandNames.CommitAndPR, project, pipeline, CommandResult.Ok("delivered"));
        h.ExecutorMock.Setup(e => e.ExecuteAsync(commitAndPr, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                new RunShortfall(
                    [new PhaseProgress("p0001a", "a", PhaseRunState.Done)],
                    [new PhaseProgress("p0001b", "b", PhaseRunState.HandedBack)],
                    "a premise of p0001b no longer holds").MarkDelivered(pipeline);
                return Task.FromResult(CommandResult.Ok("delivered"));
            });

        var result = await h.Sut.ExecuteAsync(
            [CommandNames.CheckPhasePremises, CommandNames.CommitAndPR],
            project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the phases that verified reached the pull request");
        result.Message.Should().Contain("Delivered 1 of 2 phase(s)").And.Contain("p0001b");
        pipeline.Get<string>(ContextKeys.FailureReason).Should().Contain("False premise in p0001b");
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Never);
    }

    private static ICommandContext Arrange(
        PipelineExecutorTestBuilder h, string name, ResolvedProject project,
        PipelineContext pipeline, CommandResult result)
    {
        var context = new Mock<ICommandContext>().Object;
        h.FactoryMock
            .Setup(f => f.Create(It.Is<PipelineCommand>(c => c.Name == name), project, pipeline))
            .Returns(context);
        h.ExecutorMock
            .Setup(e => e.ExecuteAsync(context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return context;
    }
}
