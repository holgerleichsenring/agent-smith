using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Commands;

public partial class CommitAndPRHandlerTests
{
    [Fact]
    public async Task CommitAndPR_ARunThatChangedNothing_OpensNoPullRequest()
    {
        // 2026-11-28-db88b: no result.md is written into the repository any more, so a run that changed no code and
        // recorded nothing under .agentsmith has nothing to carry — no record-only pull request; the record is on the
        // run page.
        _sandboxMock.Reset();
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.RunId, "run-x");
        var context = new CommitAndPRContext(
            new Repository(new BranchName("fix/123"), "https://github.com/test/repo"), [],
            new Ticket(new TicketId("123"), "Fix the bug", "Description", null, "Open", "GitHub"),
            [new RepoConnection()], new TrackerConnection(), pipeline, null);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        _sourceProviderMock.Verify(s => s.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<PullRequestTicket?>()), Times.Never);
        _events.Events.OfType<PullRequestOutcomeEvent>().Single().Status.Should().Be("no_changes");
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("its record is on the run page");
    }
}
