using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public sealed class PullRequestOutcomePublisherTests
{
    [Fact]
    public async Task PublishAsync_ARun_EmitsTheOutcomeForItsRepo()
    {
        var events = new RecordingEventPublisher();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");

        await new PullRequestOutcomePublisher(events).PublishAsync(
            pipeline, new OpenedPullRequest("sample-app", "https://stub.test/pull/1", OpenStatus.Opened), CancellationToken.None);

        var outcome = events.Events.OfType<PullRequestOutcomeEvent>().Single();
        outcome.Should().Match<PullRequestOutcomeEvent>(e =>
            e.RunId == "run-1" && e.Repo == "sample-app" && e.Status == "opened" && e.Url == "https://stub.test/pull/1");
    }

    [Fact]
    public async Task PublishAsync_NoRunId_EmitsNothing()
    {
        var events = new RecordingEventPublisher();

        await new PullRequestOutcomePublisher(events).PublishAsync(
            new PipelineContext(), new OpenedPullRequest("sample-app", null, OpenStatus.SkippedNoChanges), CancellationToken.None);

        events.Events.Should().BeEmpty();
    }
}
