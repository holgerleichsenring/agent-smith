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

    private RequirementsMetStart Start(ResolvedProject project)
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { [project.Name] = project },
            PipelineTriggers = PipelineTriggerMap.Empty,
        };
        _scopes = new ServiceCollection().AddScoped(_ => FiledWorkDoubles.Move(config)).BuildServiceProvider();
        return new RequirementsMetStart(_facts, _facts, TicketRequirementDoubles.Of(_facts, _approvals), _tracker,
            _scopes.GetRequiredService<IServiceScopeFactory>(), Fallback(config), NullLogger<RequirementsMetStart>.Instance);
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
