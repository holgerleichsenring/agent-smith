using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-18-550aa: the done status comes from the project, so a request that came through the job queue
/// still moves a GitHub ticket to its configured label instead of closing the issue.</summary>
public partial class CommitAndPRHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ContextFromBuilder_FinalizesWithTheProjectsDoneStatus()
    {
        var pipeline = NewPipelineWithSandbox();
        var queued = JsonSerializer.Deserialize<PipelineRequest>(JsonSerializer.Serialize(new PipelineRequest(
            "p1", "code", Context: new() { [ContextKeys.NeedsClarificationStatus] = "Question" })))!;
        foreach (var (key, value) in queued.Context!) pipeline.Set(key, value);
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("fix/123"), "https://github.com/test/repo"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection()]);
        pipeline.Set<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, [new(new FilePath("README.md"), "content", "Created")]);
        pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId("123"), "Fix the bug", "Description", null, "Open", "GitHub"));
        var project = new ResolvedProject
        {
            Name = "p1",
            Tracker = new TrackerConnection { Type = TrackerType.GitHub },
            GithubTrigger = new WebhookTriggerConfig { DoneStatus = "in-review" },
        };

        var context = (CommitAndPRContext)new CommitAndPRContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.CommitAndPR), project, pipeline);
        await _sut.ExecuteAsync(context, CancellationToken.None);

        _ticketProviderMock.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "123"), It.IsAny<string>(), "in-review", It.IsAny<CancellationToken>()), Times.Once);
    }
}
