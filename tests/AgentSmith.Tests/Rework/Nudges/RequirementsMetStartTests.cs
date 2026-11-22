using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.SpecDialog.Continuation;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Nudges;

/// <summary>
/// 2026-10-14-dbceh: a RequirementsMet nudge on a never-run ticket starts it — tagged on GitHub, moved on Jira,
/// then claimed through the poller's route — only when every requirement landed, its filing is complete and its
/// approver could start runs; never a ticket a person marked done by hand or closed. With an attempt, or with
/// any other origin, the handler is unchanged.
/// </summary>
public sealed class RequirementsMetStartTests : IDisposable
{
    private const string Tag = "agentsmith-alpha";
    private readonly InMemoryTicketFacts _facts = new();
    private readonly InMemorySpecApprovalStore _approvals = new();
    private readonly FakeTicketTracker _tracker = new() { Status = "open" };
    private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
    private readonly AgentSmith.Tests.Series.InMemorySeriesRunStore _series = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true }; // 9719d: the set reads loop LatestAsync
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly Mock<IRedisClaimLock> _locks = Locks();
    private readonly Mock<IReworkNudges> _nudges = new();

    private static Mock<IRedisClaimLock> Locks()
    {
        var locks = new Mock<IRedisClaimLock>();
        locks.Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync("token");
        return locks;
    }
    private ServiceProvider? _scopes;

    public void Dispose() => _scopes?.Dispose();

    [Fact]
    public async Task RequirementsMetStart_LastRequirementLanded_TagsTheGitHubTicketAndClaimsIt()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");

        (await StartAsync(GitHub, "42")).Should().Be(ReworkNudgeDisposition.Finish);

        _tracker.Labels.Should().Equal(Tag);
        Claimed(Times.Once());
    }

    [Fact]
    public async Task RequirementsMetStart_LastRequirementLanded_MovesTheJiraTicketIntoItsTriggerStatus()
    {
        _tracker.Status = "To Do";
        await HeldAsync(Jira, "DPG-42", "DPG-41");
        Land(Jira, "DPG-41");

        await StartAsync(Jira, "DPG-42");

        _tracker.Moves.Should().Equal("Ready");
        Claimed(Times.Once());
    }

    [Fact]
    public async Task RequirementsMetStart_StatusAlreadyTriggers_ClaimsThroughTheFallbackSpawn()
    {
        _tracker.Labels.Add(Tag);
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");

        await StartAsync(GitHub, "42");

        _tracker.Moves.Should().BeEmpty();
        Claimed(Times.Once(), "in webhook mode nothing else delivers a ticket that already sits in a trigger status");
    }

    [Fact]
    public async Task RequirementsMetStart_OneRequirementStillUnlanded_StartsNothing()
    {
        await HeldAsync(GitHub, "42", "40", "41");
        Land(GitHub, "41");

        await StartAsync(GitHub, "42");

        Untouched();
    }

    // 2026-11-19-1b72a: a ticket of a paused series is not moved into a trigger status it could not be claimed from; the
    // resume starts it.
    [Fact]
    public async Task RequirementsMetStart_SeriesPaused_StartsNothing()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        await _series.OpenAsync(new AgentSmith.Contracts.Series.SeriesRunRecord("sr-1", "conv-1", GitHub.Name, "operator", true,
            AgentSmith.Contracts.Series.SeriesRunStates.Paused,
            [new AgentSmith.Contracts.Series.SeriesTicket("42", "#42", "work", false, DateTimeOffset.UnixEpoch)], DateTimeOffset.UnixEpoch), default);

        (await StartAsync(GitHub, "42")).Should().Be(ReworkNudgeDisposition.Finish);

        Untouched();
    }

    [Fact]
    public async Task SeriesResumeStart_RequirementsLanded_StartsAndClaimsThroughThePollersRoute()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");

        var start = await SeriesActs(GitHub).StartIfReadyAsync(GitHub.Name, "42", default);

        start.Started.Should().BeTrue(start.Text);
        _tracker.Labels.Should().Equal(Tag);
        Claimed(Times.Once());
    }

    [Fact]
    public async Task SeriesResumeStart_StatusAlreadyTriggers_ClaimsThroughTheFallbackSpawn()
    {
        _tracker.Labels.Add(Tag);
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");

        (await SeriesActs(GitHub).StartIfReadyAsync(GitHub.Name, "42", default)).Started.Should().BeTrue();

        Claimed(Times.Once(), "in webhook mode a ticket the pause held in its trigger status is claimed by the resume");
    }

    [Fact]
    public async Task SeriesResumeStart_WaitsForAStartByName_IsLeftToThatStart()
    {
        await HeldAsync(GitHub, "42", "41", startsWhenMet: false);
        Land(GitHub, "41");

        var start = await SeriesActs(GitHub).StartIfReadyAsync(GitHub.Name, "42", default);

        start.Should().Be(new AgentSmith.Contracts.Series.SeriesStart(false, "Ticket 42 is held: it waits for a start by name."));
        Untouched();
    }

    [Fact]
    public async Task SeriesResumeStart_GreenPushPrerequisite_StartsBeforeItLands()
    {
        var project = GitHub with { RequiresStart = RequiresStart.OnGreenPush };
        await HeldAsync(project, "42", "41");
        _attempts.Setup(a => a.LatestAsync(project.Name, "41", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-41", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true) { OpenedPullRequest = true });

        (await SeriesActs(project).StartIfReadyAsync(project.Name, "42", default)).Started.Should().BeTrue();

        Claimed(Times.Once());
    }

    [Fact]
    public async Task SeriesResumeStart_ClosedTicket_IsLeftAlone()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        await _facts.SetAsync(new TicketClosedMark(GitHub.Tracker.Name, Key(GitHub, "42"), "42", "alpha", DateTimeOffset.UnixEpoch,
            "Ada", "superseded", [], false), default);

        (await SeriesActs(GitHub).StartIfReadyAsync(GitHub.Name, "42", default)).Started.Should().BeFalse();

        Untouched();
    }

    [Fact]
    public async Task SeriesResumeStart_AnotherPausedSeriesHoldsIt_IsNotStarted()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        await _series.OpenAsync(new AgentSmith.Contracts.Series.SeriesRunRecord("sr-2", "conv-2", GitHub.Name, "operator", true,
            AgentSmith.Contracts.Series.SeriesRunStates.Paused,
            [new AgentSmith.Contracts.Series.SeriesTicket("42", "#42", "work", false, DateTimeOffset.UnixEpoch)], DateTimeOffset.UnixEpoch), default);

        (await SeriesActs(GitHub).StartIfReadyAsync(GitHub.Name, "42", default)).Text.Should().Be("Ticket 42 is held: series sr-2 is paused.");

        Untouched();
    }

    // 2026-11-19-1b72a: a claimed start queues a code run; a second wake (or a resume) for the same ticket finds it
    // under way and starts nothing — the double start seen under on_green_push.
    [Fact]
    public async Task RequirementsMetStart_TwoWakesForOneTicket_StartItOnce()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        Queues();

        await StartAsync(GitHub, "42");
        await StartAsync(GitHub, "42");

        Claimed(Times.Once());
    }

    [Fact]
    public async Task SeriesStartTicket_AfterAWake_DoesNotStartAgain()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        Queues();
        await StartAsync(GitHub, "42");

        (await SeriesActs(GitHub).StartAsync(GitHub.Name, "42", default)).Should().Be(
            "Ticket 42 is under way: a start of it is queued already; retry or tell it instead.");
        Claimed(Times.Once());
    }

    [Fact]
    public async Task SeriesResumeStart_LiveLease_IsUnderWay()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        _leases.Setup(l => l.GetByTicketAsync("alpha", new AgentSmith.Domain.Models.TicketId("42"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("alpha", new AgentSmith.Domain.Models.TicketId("42"), "run-1", null));

        (await SeriesActs(GitHub).StartIfReadyAsync(GitHub.Name, "42", default)).Text.Should().Be("Ticket 42 is held: a run holds it now.");
        Untouched();
    }

    // A resume and a wake both start a waiting ticket: the resume takes the nudge's ticket lock, so while a wake holds it
    // the resume starts nothing and the wake's start is the one.
    [Fact]
    public async Task SeriesResumeStart_WhileTheNudgeHoldsTheTicketsLock_StartsNothing()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        _locks.Setup(l => l.TryAcquireAsync(ReworkNudgeWorker.LockKey("alpha", "42"), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        (await SeriesActs(GitHub).StartIfReadyAsync(GitHub.Name, "42", default)).Started.Should().BeFalse();

        Untouched();
        _nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.TicketId == "42" && r.Origin == ReworkNudgeOrigin.RequirementsMet),
            It.IsAny<CancellationToken>()), Times.Once, "the start is queued behind the nudge, never lost");
    }

    // The nudge a busy resume queued starts a ticket without requirements only while a running series whose approver may
    // start runs owns it — read when the nudge is served, so a series stopped since starts nothing.
    [Theory]
    [InlineData(AgentSmith.Contracts.Series.SeriesRunStates.Running, true, true)]
    [InlineData(AgentSmith.Contracts.Series.SeriesRunStates.Stopped, true, false)]
    [InlineData(AgentSmith.Contracts.Series.SeriesRunStates.Running, false, false)]
    public async Task RequirementsMetStart_TicketWithoutRequirements_StartsOnlyForARunningSeriesThatMayStartRuns(
        string state, bool mayStartRuns, bool starts)
    {
        await _approvals.SaveAsync(ApprovedSets.Record(Key(GitHub, "42"), DateTimeOffset.UnixEpoch, tracker: GitHub.Tracker.Name), default);
        await _series.OpenAsync(new AgentSmith.Contracts.Series.SeriesRunRecord("sr-1", "conv-1", GitHub.Name, "operator", mayStartRuns,
            state, [new AgentSmith.Contracts.Series.SeriesTicket("42", "#42", "work", false, DateTimeOffset.UnixEpoch)], DateTimeOffset.UnixEpoch), default);

        await StartAsync(GitHub, "42");

        Claimed(starts ? Times.Once() : Times.Never());
    }

    // The claim the fallback spawn makes queues the ticket's code run, as the capacity queue does.
    private void Queues()
    {
        var queued = false;
        _spawn.Setup(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "code",
                It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
                It.IsAny<Dictionary<string, string>?>()))
            .Callback(() => queued = true).ReturnsAsync(new SpawnResult([]));
        _attempts.Setup(a => a.HasQueuedAsync("alpha", "42", It.IsAny<CancellationToken>())).ReturnsAsync(() => queued);
    }

    [Fact]
    public async Task RequirementsMetStart_FilingIncomplete_StartsNothing()
    {
        _facts.Edges.Add(Edge(GitHub, "42", "41"));
        Land(GitHub, "41");

        await StartAsync(GitHub, "42");

        Untouched();
    }

    [Fact]
    public async Task RequirementsMetStart_ApproverLackedRunsControl_StaysHeldAndLogsIt()
    {
        await HeldAsync(GitHub, "42", "41", startsWhenMet: false);
        Land(GitHub, "41");

        (await StartAsync(GitHub, "42")).Should().Be(ReworkNudgeDisposition.Finish);

        Untouched();
    }

    [Fact]
    public async Task RequirementsMetStart_DependentAlreadyLandedByHand_StartsNothing()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        Land(GitHub, "42", TicketLandingKinds.ByHand);

        await StartAsync(GitHub, "42");

        Untouched();
    }

    // 2026-10-15-114fa: a person closed the ticket here; its requirements landing does not start it.
    [Fact]
    public async Task RequirementsMetStart_ClosedTicket_IsNotStarted()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        await _facts.SetAsync(new TicketClosedMark(GitHub.Tracker.Name, Key(GitHub, "42"), "42", "alpha", DateTimeOffset.UnixEpoch,
            "Ada", "superseded", [], false), default);

        (await StartAsync(GitHub, "42")).Should().Be(ReworkNudgeDisposition.Finish);

        Untouched();
    }

    [Fact]
    public async Task RequirementsMetStart_DependentClosedOnJira_IsNotMovedOrClaimed()
    {
        _tracker.Status = "Done";
        _tracker.IsClosed = true;
        await HeldAsync(Jira, "DPG-42", "DPG-41");
        Land(Jira, "DPG-41");

        await StartAsync(Jira, "DPG-42");

        Untouched();
    }

    [Fact]
    public async Task ReworkNudgeHandler_RequirementsMetOnTicketWithAnAttempt_TakesTheExistingPath()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("alpha", "42", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", DateTimeOffset.UnixEpoch, true));
        var pending = new Mock<IReworkPendingActs>();

        (await HandleAsync(ReworkNudgeOrigin.RequirementsMet, attempts.Object, pending.Object)).Should().Be(ReworkNudgeDisposition.Finish);

        pending.Verify(p => p.NewestAsync(GitHub, "42", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce,
            "an attempted ticket's nudge reads the acts, as any nudge does");
        Untouched();
    }

    [Fact]
    public async Task ReworkNudgeHandler_OtherOriginOnTicketWithoutAttempt_StillFinishes()
    {
        await HeldAsync(GitHub, "42", "41");
        Land(GitHub, "41");

        (await HandleAsync(ReworkNudgeOrigin.Operator, Mock.Of<IPreviousAttemptReader>(), Mock.Of<IReworkPendingActs>()))
            .Should().Be(ReworkNudgeDisposition.Finish);

        Untouched();
    }

    [Fact]
    public async Task ReworkNudgeOrigin_RequirementsMetMergedWithOperator_IsServedAsRequirementsMet()
    {
        using var store = new ServerStateStore();
        ReworkNudgeRepository Repo() => store.Services.CreateScope().ServiceProvider.GetRequiredService<ReworkNudgeRepository>();
        await Repo().EnqueueAsync(new ReworkNudgeRequest("alpha", "42", ReworkNudgeOrigin.RequirementsMet), default);
        await Repo().EnqueueAsync(new ReworkNudgeRequest("alpha", "42", ReworkNudgeOrigin.Operator), default);

        (await Repo().ClaimDueAsync(10, default)).Should().ContainSingle().Which.Origin.Should().Be(ReworkNudgeOrigin.RequirementsMet);
    }

    private static ResolvedProject GitHub { get; } = Project(TrackerType.GitHub, "gh");
    private static ResolvedProject Jira { get; } = Project(TrackerType.Jira, "jt");

    private static ResolvedProject Project(TrackerType type, string tracker)
    {
        var resolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = Tag };
        return new ResolvedProject
        {
            Name = "alpha", DefaultPipeline = "code",
            Tracker = new TrackerConnection { Name = tracker, Type = type },
            Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }],
            GithubTrigger = type == TrackerType.GitHub
                ? new WebhookTriggerConfig { DefaultPipeline = "code", TriggerStatuses = ["open"], ProjectResolution = resolution } : null,
            JiraTrigger = type == TrackerType.Jira
                ? new JiraTriggerConfig { DefaultPipeline = "code", TriggerStatuses = ["Ready"], ProjectResolution = resolution } : null,
        };
    }

    private static string Key(ResolvedProject project, string id) =>
        AgentSmith.Contracts.Specs.TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), id).Value;

    private static TicketRequirementEdge Edge(ResolvedProject project, string ticket, string required, bool startsWhenMet = true) =>
        new(project.Tracker.Name, Key(project, ticket), ticket, project.Name, Key(project, required), required, startsWhenMet, default, "s-1");

    private async Task HeldAsync(ResolvedProject project, string ticket, string first, string? second = null, bool startsWhenMet = true)
    {
        _facts.Edges.Add(Edge(project, ticket, first, startsWhenMet));
        if (second is not null) _facts.Edges.Add(Edge(project, ticket, second, startsWhenMet));
        await _approvals.SaveAsync(ApprovedSets.Record(Key(project, ticket), DateTimeOffset.UnixEpoch, tracker: project.Tracker.Name), default);
    }

    private void Land(ResolvedProject project, string ticket, string kind = TicketLandingKinds.Merged) =>
        _facts.Landed.Add(new TicketLandingFact(project.Tracker.Name, Key(project, ticket), ticket, project.Name, kind,
            DateTimeOffset.UnixEpoch, null, null, null));

    private RequirementsMetStart Start(ResolvedProject project) => new(Waiting(project));

    // 2026-11-19-1b72a: the one start the wake and a series' resume share.
    private WaitingTicketStart Waiting(ResolvedProject project)
    {
        var config = Config(project);
        _scopes = new ServiceCollection().AddScoped(_ => ActSteps.Start(config, FiledWorkDoubles.Move(config), _tracker)).BuildServiceProvider();
        return new WaitingTicketStart(_facts, new TicketStartHold(_facts, _facts, _tracker, NullLogger<TicketStartHold>.Instance),
            new AgentSmith.Application.Services.Claim.UnmetTicketRequirements(_facts, _facts, _approvals,
                new AgentSmith.Application.Services.Claim.GreenPrerequisites(_facts, _attempts.Object, _facts), _series),
            _scopes.GetRequiredService<IServiceScopeFactory>(), Fallback(config), _attempts.Object, _leases.Object,
            NullLogger<WaitingTicketStart>.Instance);
    }

    private static AgentSmithConfig Config(ResolvedProject project) => new()
    {
        Projects = new Dictionary<string, ResolvedProject> { [project.Name] = project },
        PipelineTriggers = PipelineTriggerMap.Empty,
    };

    private AgentSmith.Server.Services.Series.SeriesTicketActs SeriesActs(ResolvedProject project)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config(project));
        var waiting = Waiting(project);
        return new(new AgentSmith.Server.Services.Tickets.TicketProjects(loader.Object, new ServerContext("c.yml")),
            TicketRequirementDoubles.Of(_facts, _approvals), null!, _attempts.Object, null!, null!, null!, null!, waiting,
            _nudges.Object, _locks.Object);
    }

    private ReworkFallbackSpawn Fallback(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return new ReworkFallbackSpawn(loader.Object, new ServerContext("c.yml"), _tracker,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), FiledWorkDoubles.Resolver(), _spawn.Object,
            NullLogger<ReworkFallbackSpawn>.Instance);
    }

    private Task<ReworkNudgeDisposition> StartAsync(ResolvedProject project, string ticket) =>
        Start(project).StartAsync(project, Nudge(ticket, ReworkNudgeOrigin.RequirementsMet), CancellationToken.None);

    private async Task<ReworkNudgeDisposition> HandleAsync(
        ReworkNudgeOrigin origin, IPreviousAttemptReader attempts, IReworkPendingActs pending)
    {
        using var store = new ServerStateStore(configure: ClosedTicketFollowUps.Register);
        var speech = new ReworkSpeech(store.ScopeFactory, _tracker, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);
        var start = Start(GitHub);
        var handler = ReworkHandlers.Of(Mock.Of<IActiveRunLease>(), attempts, pending, Mock.Of<IReworkEntry>(), speech,
            Fallback(new AgentSmithConfig()), new ReworkWithheld(store.ScopeFactory, speech), Mock.Of<IRunCheckpointStore>(),
            ClosedTicketFollowUps.Of(store.ScopeFactory, _tracker, speech), start: start);
        return await handler.HandleAsync(GitHub, Nudge("42", origin), CancellationToken.None);
    }

    private static ClaimedReworkNudge Nudge(string ticket, ReworkNudgeOrigin origin) =>
        new("alpha", ticket, origin, null, null, 1, "tok", 0);

    private void Claimed(Times times, string because = "") =>
        _spawn.Verify(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), "code",
            It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
            It.IsAny<Dictionary<string, string>?>()), times, because);

    private void Untouched()
    {
        _tracker.Labels.Should().BeEmpty();
        _tracker.Moves.Should().BeEmpty();
        Claimed(Times.Never());
    }
}
