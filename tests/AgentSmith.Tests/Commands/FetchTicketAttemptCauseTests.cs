using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-10-13-9a59d: the run records the act it served on its fetched event, from its own read —
/// whichever claimer won the lease, and with no nudge in hand.
/// </summary>
public sealed class FetchTicketAttemptCauseTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private const string PrUrl = "https://github.com/o/api/pull/4";

    [Fact]
    public async Task AttemptCause_AnotherReplicaClaimed_StillRecordsThePrReview()
    {
        var published = new List<RunEvent>();
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback((RunEvent e, CancellationToken _) => published.Add(e)).Returns(Task.CompletedTask);
        var handler = Handler(events.Object);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "alpha");
        pipeline.Set(ContextKeys.RunId, "run-2");

        await handler.ExecuteAsync(new FetchTicketContext(
            new TicketId("42"), new TrackerConnection { Name = "gh", Type = TrackerType.GitHub }, pipeline), default);

        var fetched = published.OfType<TicketFetchedEvent>().Single();
        fetched.Cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.PullRequest, "alice", Start.AddMinutes(30),
            new AttemptSource(AttemptSourceKinds.Review, PrUrl, "PRR_1")));
        fetched.WebUrl.Should().Be("https://github.com/o/api/issues/42");
        fetched.TicketFingerprint.Should().HaveLength(64);
    }

    // 2026-10-13-9a59g: the ticket's criteria travel on the fetched event, converted as the description is.
    [Fact]
    public async Task FetchTicketHandler_PublishesAcceptanceCriteria()
    {
        const string criteria = "<ul><li>It <b>builds</b></li><li>It ships</li></ul>";
        var published = new List<RunEvent>();
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback((RunEvent e, CancellationToken _) => published.Add(e)).Returns(Task.CompletedTask);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "alpha");
        pipeline.Set(ContextKeys.RunId, "run-2");

        await Handler(events.Object, criteria).ExecuteAsync(new FetchTicketContext(
            new TicketId("42"), new TrackerConnection { Name = "gh", Type = TrackerType.GitHub }, pipeline), default);

        var fetched = published.OfType<TicketFetchedEvent>().Single();
        fetched.AcceptanceCriteria.Should().Be(TicketHtmlConverter.ToText(criteria));
        fetched.AcceptanceCriteria.Should().Contain("builds").And.NotContain("<b>");
    }

    private static FetchTicketHandler Handler(IEventPublisher events, string? criteria = null)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(new TicketId("42"), "Widget", "Body", criteria, "open", "GitHub",
                webUrl: "https://github.com/o/api/issues/42"));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var previous = new Mock<IPreviousAttemptReader>();
        previous.Setup(p => p.LatestAsync("alpha", "42", "run-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", Start, true));
        var runContext = new Mock<IRunContextAccessor>();
        runContext.SetupGet(r => r.CurrentRunId).Returns("run-2");
        var identity = new TrackerIdentity(factory.Object, NullLogger<TrackerIdentity>.Instance);
        return new FetchTicketHandler(
            factory.Object, events, runContext.Object,
            new TicketExtrasFetcher(previous.Object, NullLogger<TicketExtrasFetcher>.Instance),
            new OwnReadOfThePullRequest(),
            new EpicGroundFetcher(new EpicParentReader(NullLogger<EpicParentReader>.Instance)),
            NullLogger<FetchTicketHandler>.Instance,
            attemptCause: new AttemptCauseReader(identity, ApprovedSetDoubles.Resolver(), NullLogger<AttemptCauseReader>.Instance));
    }

    /// <summary>The run's own read of the previous attempt's pull request: a standing Request changes.</summary>
    private sealed class OwnReadOfThePullRequest : IPrReviewFeedbackFetcher
    {
        public Task FetchAsync(PipelineContext pipeline, CancellationToken cancellationToken)
        {
            var note = new PrReviewNote(new AgentSmith.Contracts.Webhooks.PrCommentAuthor("u", "o/api", "alice", "alice"),
                false, false, Start.AddMinutes(30), string.Empty) { HostId = "PRR_1" };
            ReworkActReader.ApplyPullRequest(pipeline, PrStandingAct.NewerAct(note, null, PrUrl)!);
            return Task.CompletedTask;
        }
    }
}
