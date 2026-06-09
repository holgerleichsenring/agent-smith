using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Polling;

/// <summary>
/// p0140c: TrackerPoller pulls open tickets from one tracker, routes each via
/// IEnvelopeProjectResolver, then spawns per matched project via ISpawnPipelineRunsUseCase.
/// Returns a PollResult counts summary (no more ClaimRequest lists). These tests cover
/// the matching/dedup/filter behaviour against the new contracts.
/// </summary>
public sealed class TrackerPollerTests
{
    private const string TrackerName = "shared-tr";

    [Fact]
    public async Task PollAsync_NoTickets_ReturnsEmpty()
    {
        var harness = new Harness();
        harness.WithPendingTickets().WithDiscoveredTickets();

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.PolledTickets.Should().Be(0);
        result.Should().BeEquivalentTo(PollResult.Empty());
        harness.SpawnCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PollAsync_TwoProjectsShareTracker_TicketWithAlphaTag_SpawnsOnlyAlpha()
    {
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        harness.WithTaggedProject("alpha", "alpha-tag");
        harness.WithTaggedProject("beta", "beta-tag");
        harness.WithPendingTickets(MakeTicket("1", labels: new[] { "alpha-tag" }));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.PolledTickets.Should().Be(1);
        result.MatchedProjects.Should().Be(1);
        result.Spawned.Should().Be(1);
        harness.SpawnCallCount.Should().Be(1);
        harness.SpawnedProjectNames.Should().BeEquivalentTo(new[] { "alpha" });
    }

    [Fact]
    public async Task PollAsync_TicketStatusNotInProjectTriggerStatuses_SkipsProject()
    {
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        harness.WithTaggedProject("alpha", "alpha-tag",
            triggerStatuses: new List<string> { "open" });
        harness.WithPendingTickets(MakeTicket("1", labels: new[] { "alpha-tag" }, status: "closed"));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.MatchedProjects.Should().Be(1);
        result.StatusFiltered.Should().Be(1);
        result.Spawned.Should().Be(0);
        harness.SpawnCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PollAsync_ZeroMatch_LogsAndCountsNotSpawned()
    {
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        // No projects configured -> resolver returns empty.
        harness.WithPendingTickets(MakeTicket("1", labels: new[] { "stray-tag" }));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.PolledTickets.Should().Be(1);
        result.ZeroMatched.Should().Be(1);
        result.Spawned.Should().Be(0);
        harness.SpawnCallCount.Should().Be(0);
        // Zero-match comment is webhook-only path; the poller must not call UpdateStatusAsync.
        harness.UpdateStatusCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PollAsync_TwoProjectsMatchSameTicket_BothSpawn()
    {
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        harness.WithTaggedProject("alpha", "shared-tag");
        harness.WithTaggedProject("beta", "shared-tag");
        harness.WithPendingTickets(MakeTicket("1", labels: new[] { "shared-tag" }));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.MatchedProjects.Should().Be(2);
        result.Spawned.Should().Be(2);
        harness.SpawnCallCount.Should().Be(2);
        harness.SpawnedProjectNames.Should().BeEquivalentTo(new[] { "alpha", "beta" });
    }

    [Fact]
    public async Task PollAsync_TrackerWithNoMatchingProjects_AllZeroMatched()
    {
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        // Project exists but is wired to a different tracker type -> resolver still
        // looks at projects, but the project's only trigger (jira) won't match a
        // github envelope because the tag value doesn't appear on either ticket.
        harness.WithTaggedProject("only-jira-project", "jira-only-tag", platform: TrackerType.Jira);

        harness.WithPendingTickets(
            MakeTicket("1", labels: new[] { "unrelated-1" }),
            MakeTicket("2", labels: new[] { "unrelated-2" }),
            MakeTicket("3", labels: new[] { "unrelated-3" }));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.PolledTickets.Should().Be(3);
        result.ZeroMatched.Should().Be(3);
        result.Spawned.Should().Be(0);
        harness.SpawnCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PollAsync_MergesPendingAndDiscovered_DedupesByTicketId()
    {
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        harness.WithTaggedProject("alpha", "tag");

        harness
            .WithPendingTickets(
                MakeTicket("1", labels: new[] { "tag" }),
                MakeTicket("2", labels: new[] { "tag" }))
            .WithDiscoveredTickets(
                MakeTicket("2", labels: new[] { "tag" }),
                MakeTicket("3", labels: new[] { "tag" }));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.PolledTickets.Should().Be(3);
        result.Spawned.Should().Be(3);
        harness.SpawnedTicketIds.Should().BeEquivalentTo(new[] { "1", "2", "3" });
    }

    [Fact]
    public async Task PollAsync_LifecycleTags_NoLongerGate_AllClaimed()
    {
        // p0262: lifecycle tags are pure markers — enqueued/in-progress/done/failed no
        // longer drop a ticket from discovery (the LifecyclePollFilter is gone). All four
        // are claimed; in-flight gating is the LEASE (see PollAsync_HeldLease_SkipsInFlight),
        // not the tag.
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        harness.WithTaggedProject("alpha", "tag");

        harness.WithDiscoveredTickets(
            MakeTicket("1", labels: new[] { "tag" }),
            MakeTicket("2", labels: new[] { "tag", "agent-smith:enqueued" }),
            MakeTicket("3", labels: new[] { "tag", "agent-smith:in-progress" }),
            MakeTicket("4", labels: new[] { "tag", "agent-smith:failed" }));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.Spawned.Should().Be(4);
        harness.SpawnedTicketIds.Should().BeEquivalentTo(new[] { "1", "2", "3", "4" });
    }

    [Fact]
    public async Task PollAsync_HeldLease_SkipsInFlight()
    {
        // p0262: a ticket with a held lease (a live run, or a dead one the reaper has not
        // released yet) is skipped — the lease, not a tag, is the in-flight gate.
        var harness = new Harness();
        harness.WithSharedTracker(TrackerType.GitHub);
        harness.WithTaggedProject("alpha", "tag");
        harness.WithDiscoveredTickets(
            MakeTicket("1", labels: new[] { "tag" }),
            MakeTicket("2", labels: new[] { "tag" }));
        // ticket 2 holds a lease → in-flight → skipped; ticket 1 has none → claimed.
        harness.Lease
            .Setup(l => l.GetByTicketAsync("alpha", It.Is<TicketId>(t => t.Value == "2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("alpha", new TicketId("2"), "run-x", JobId: null));

        var result = await harness.Build().PollAsync(CancellationToken.None);

        result.Spawned.Should().Be(1);
        harness.SpawnedTicketIds.Should().BeEquivalentTo(new[] { "1" });
    }

    // ---------- helpers ----------

    private static Ticket MakeTicket(string id, string[] labels, string status = "open")
        => new(new TicketId(id), $"title-{id}", "desc", null, status, "GitHub", labels);

    private sealed class Harness
    {
        public TrackerConnection Tracker { get; private set; } = new()
        {
            Name = TrackerName,
            Type = TrackerType.GitHub,
            Polling = new PollingConfig { Enabled = true, IntervalSeconds = 60 }
        };

        private readonly Dictionary<string, ResolvedProject> _projects = new();
        private readonly List<Ticket> _pending = new();
        private readonly List<Ticket> _discovered = new();

        public Mock<ITicketProvider> Provider { get; } = new();
        public Mock<ISpawnPipelineRunsUseCase> Spawn { get; } = new();
        // p0262: in-flight gating is the lease. Default (Moq) → GetByTicketAsync returns
        // null = no lease = not in-flight, so nothing is skipped unless a test sets one.
        public Mock<IActiveRunLease> Lease { get; } = new();

        public List<string> SpawnedProjectNames { get; } = new();
        public List<string> SpawnedTicketIds { get; } = new();
        public int SpawnCallCount => SpawnedProjectNames.Count;
        public int UpdateStatusCallCount { get; private set; }

        public Harness WithSharedTracker(TrackerType type)
        {
            Tracker = Tracker with { Type = type };
            return this;
        }

        public Harness WithTaggedProject(
            string name,
            string tag,
            List<string>? triggerStatuses = null,
            TrackerType? platform = null)
        {
            var actualPlatform = platform ?? Tracker.Type;
            var trigger = new WebhookTriggerConfig
            {
                ProjectResolution = new ProjectResolutionConfig
                {
                    Strategy = ResolutionStrategy.Tag,
                    Value = tag,
                },
                DefaultPipeline = "fix-bug",
                TriggerStatuses = triggerStatuses ?? new List<string>(),
            };
            var project = new ResolvedProject
            {
                Name = name,
                Tracker = Tracker,
                DefaultPipeline = "fix-bug",
                Repos = new[] { new RepoConnection { Name = name + "-repo", Url = $"https://example/{name}" } },
                GithubTrigger    = actualPlatform == TrackerType.GitHub      ? trigger : null,
                GitlabTrigger    = actualPlatform == TrackerType.GitLab      ? trigger : null,
                AzuredevopsTrigger = actualPlatform == TrackerType.AzureDevOps ? trigger : null,
                JiraTrigger      = actualPlatform == TrackerType.Jira        ? new JiraTriggerConfig
                {
                    ProjectResolution = trigger.ProjectResolution,
                    DefaultPipeline = trigger.DefaultPipeline,
                    TriggerStatuses = trigger.TriggerStatuses,
                } : null,
            };
            _projects[name] = project;
            return this;
        }

        public Harness WithPendingTickets(params Ticket[] tickets)
        {
            _pending.Clear();
            _pending.AddRange(tickets);
            return this;
        }

        public Harness WithDiscoveredTickets(params Ticket[] tickets)
        {
            _discovered.Clear();
            _discovered.AddRange(tickets);
            return this;
        }

        public TrackerPoller Build()
        {
            Provider.Setup(p => p.ListByLifecycleStatusAsync(
                    TicketLifecycleStatus.Pending, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _pending.ToArray());
            Provider.Setup(p => p.ListOpenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _discovered.ToArray());
            Provider.Setup(p => p.UpdateStatusAsync(
                    It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() => { UpdateStatusCallCount++; return Task.CompletedTask; });

            var factory = new Mock<ITicketProviderFactory>();
            factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(Provider.Object);

            Spawn.Setup(s => s.ExecuteAsync(
                    It.IsAny<AgentSmithConfig>(),
                    It.IsAny<ResolvedProject>(),
                    It.IsAny<string>(),
                    It.IsAny<IncomingTicketEnvelope>(),
                    It.IsAny<WebhookTriggerConfig>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Dictionary<string, string>?>()))
                .ReturnsAsync(new SpawnResult(Array.Empty<ClaimResult>()))
                .Callback<AgentSmithConfig, ResolvedProject, string, IncomingTicketEnvelope, WebhookTriggerConfig, CancellationToken, Dictionary<string, string>?>(
                    (_, p, _, env, _, _, _) =>
                    {
                        SpawnedProjectNames.Add(p.Name);
                        SpawnedTicketIds.Add(env.TicketId ?? string.Empty);
                    });

            var config = new AgentSmithConfig
            {
                Trackers = new Dictionary<string, TrackerConnection> { [Tracker.Name] = Tracker },
                Projects = _projects,
            };

            // Use the real ProjectResolver as the envelope resolver — its behaviour is
            // load-bearing for the matching tests and a hand-rolled fake would just
            // re-encode its logic.
            var envelopeResolver = new ProjectResolver();

            return new TrackerPoller(
                Tracker, config, factory.Object, envelopeResolver, Spawn.Object,
                Lease.Object,
                new NoOpSystemEventPublisher(),
                NullLogger<TrackerPoller>.Instance);
        }
    }
}
