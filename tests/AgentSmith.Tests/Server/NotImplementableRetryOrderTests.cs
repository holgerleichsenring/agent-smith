using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-09-21-1fa0: the operator retry MOVES the ticket before it drops the hold, and reports
/// what the tracker actually did. It used to clear first and transition second, discard the
/// transition's answer, and log the move unconditionally — so a tracker that offered no such
/// move left the ticket in no trigger status with no hold: claimable by nobody, explained by
/// nothing, and reported to the operator as a success.
/// </summary>
public sealed class NotImplementableRetryOrderTests
{
    private const string Project = "p1";
    private const string Ticket = "42";
    private const string Target = "Approved";

    private readonly RecordingUnmovedStore _holds = new();
    private readonly InMemorySpecSetPointerStore _pointers = new();
    private readonly CapturingLogger<NotImplementableRetryService> _logger = new();

    [Fact]
    public async Task Retry_AMoveThatLands_ClearsTheHoldAndReportsIt()
    {
        var provider = Provider(moved: true);

        var outcome = await Service(provider).RetryAsync(
            Config(Target), Ticket, CancellationToken.None);

        outcome.Should().Be(RetryOutcome.Retried);
        _holds.Cleared.Should().ContainSingle("the ticket now carries a status the poller triggers on")
            .Which.Should().Be(Ticket);
        provider.Verify(p => p.TransitionToAsync(
            new TicketId(Ticket), Target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Retry_AMoveTheTrackerRefuses_LeavesTheHoldStandingAndSaysSo()
    {
        await SeedHandbackAsync();

        var outcome = await Service(Provider(moved: false)).RetryAsync(
            Config(Target), Ticket, CancellationToken.None);

        outcome.Should().Be(RetryOutcome.TrackerRefusedTheMove);
        _holds.Cleared.Should().BeEmpty(
            "the hold is the only thing left saying why the ticket is not being picked up");
        (await Pointer())!.RepeatedHandbackCount.Should().Be(3,
            "a retry that did not happen resets nothing either");
    }

    [Fact]
    public async Task Retry_AMoveThatThrows_LeavesTheHoldStanding()
    {
        await SeedHandbackAsync();
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.TransitionToAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TF401320: the state is not allowed"));

        var act = () => Service(provider).RetryAsync(Config(Target), Ticket, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _holds.Cleared.Should().BeEmpty("the throw escapes BEFORE anything is cleared");
        (await Pointer())!.RepeatedHandbackCount.Should().Be(3);
    }

    [Fact]
    public async Task Retry_AProjectWithNoTriggerStatus_StillReportsThatAndClearsNothing()
    {
        var provider = Provider(moved: true);

        var outcome = await Service(provider).RetryAsync(
            Config(null), Ticket, CancellationToken.None);

        outcome.Should().Be(RetryOutcome.NoTriggerStatus);
        _holds.Cleared.Should().BeEmpty();
        provider.Verify(p => p.TransitionToAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Retry_TheLogLine_DoesNotAssertAMoveThatDidNotHappen()
    {
        await Service(Provider(moved: false)).RetryAsync(Config(Target), Ticket, CancellationToken.None);

        // The log is the one artefact an operator consults, and it used to state the move
        // whatever happened — which is how a retry that moved nothing read as one that did.
        _logger.Lines.Should().NotContain(line => line.Contains("moved back to", StringComparison.Ordinal));
        _logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain("no move to").And.Contain("keeps its hold");
    }

    private static Mock<ITicketProvider> Provider(bool moved)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.TransitionToAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(moved);
        return provider;
    }

    private NotImplementableRetryService Service(Mock<ITicketProvider> provider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return new NotImplementableRetryService(_pointers, _holds, factory.Object, _logger);
    }

    private Task SeedHandbackAsync() => _pointers.SaveAsync(
        Project,
        new SpecSetPointer(SpecSetKey.For("github", Ticket).Value, "repo-a", "sha", 1,
            SpecHandbackCase.NotImplementable, 3),
        CancellationToken.None);

    private Task<SpecSetPointer?> Pointer() => _pointers.GetAsync(
        Project, SpecSetKey.For("github", Ticket).Value, CancellationToken.None);

    private static ResolvedProject Config(string? triggerStatus) => new()
    {
        Name = Project,
        Repos = [new RepoConnection { Name = "repo-a" }],
        Tracker = new TrackerConnection { Name = "tracker-a", Type = TrackerType.GitHub },
        GithubTrigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "fix-bug",
            TriggerStatuses = triggerStatus is null ? [] : [triggerStatus],
            DoneStatus = "closed",
        },
    };

    /// <summary>The hold, as a recorder: which tickets were released and which kept theirs.</summary>
    private sealed class RecordingUnmovedStore : IUnmovedTicketStore
    {
        public List<string> Cleared { get; } = [];

        public Task RecordAsync(UnmovedTicketFact fact, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<UnmovedTicketFact?> FindStandingAsync(
            string project, string ticketId, string tracker, CancellationToken cancellationToken) =>
            Task.FromResult<UnmovedTicketFact?>(null);

        public Task ClearAsync(string project, string ticketId, CancellationToken cancellationToken)
        {
            Cleared.Add(ticketId);
            return Task.CompletedTask;
        }
    }
}
