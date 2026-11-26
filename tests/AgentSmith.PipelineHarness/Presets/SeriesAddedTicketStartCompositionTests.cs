using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-26-5f6e: a ticket the series master filed with add_ticket, started with start_ticket, through the REAL server
/// composition — the 'file' run, the edges, the add_ticket act on the series' record, the 'start' run and the poller's route
/// to the spawn (the harness boundary). Before, a ticket added with a requirement was held as "its filing did not finish"
/// for good, and every start answered as 'started' whatever the spawn did.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesAddedTicketStartCompositionTests
{
    private const string Project = "fixture-file";

    [Fact]
    public async Task StartTicket_ATicketAddedWithALandedRequirement_SpawnsItsRun()
    {
        await using var rig = await Rig.BuildAsync(ClaimResult.Claimed(), "run-9");
        var added = await rig.AddAsync(requires: ["1"]);
        await rig.LandAsync("1");

        var start = await rig.StartAsync(added);

        start.Should().Be(new SeriesStart(SeriesStartKind.Started, $"Ticket {added} started: run run-9.", "run-9"));
        rig.Spawned.Should().Be(1);
    }

    // Under a pipeline_from_label map a filed series ticket triggers through the approved-set stamp alone; a ticket add_ticket
    // filed has no approved set, so its start puts on the one label the map routes to code — never both, never the stamp.
    [Fact]
    public async Task StartTicket_UnderAPipelineMap_TagsTheAddedTicketWithTheOneCodeTriggerLabel()
    {
        await using var rig = await Rig.BuildAsync(ClaimResult.Claimed(), "run-8", fixture: FixturePaths.FilingLabels);
        var filed = await rig.Services.GetRequiredService<AgentSmith.Server.Services.SpecDialog.IFilingRun>()
            .RunRequestAsync(Project, SeriesFiling(), "sr-1");
        var added = await rig.AddAsync();

        var start = await rig.StartAsync(added);

        start.Kind.Should().Be(SeriesStartKind.Started, start.Text);
        string[] triggers = ["spec:approved", "agent-smith:code"];
        rig.Labels(filed.Filed[0].TicketId!).Intersect(triggers).Should().Equal("spec:approved");
        rig.Labels(added).Intersect(triggers).Should().Equal("agent-smith:code");
    }

    // start_ticket is the start a by-name edge waits for; a resume still leaves the ticket to it.
    [Fact]
    public async Task StartTicket_AnEdgeThatWaitsForAStartByName_IsStartedByIt()
    {
        await using var rig = await Rig.BuildAsync(ClaimResult.Claimed(), "run-3");
        var added = await rig.AddAsync();
        await rig.RequireAsync(added, "1", startsWhenMet: false);
        await rig.LandAsync("1");

        var resumed = await rig.Services.GetRequiredService<ISeriesTicketActs>().StartIfReadyAsync(Project, added, default);
        var start = await rig.StartAsync(added);

        resumed.Should().Be(SeriesStart.Refused($"Ticket {added} was not started: it waits for a start by name; start_ticket starts it."));
        start.Kind.Should().Be(SeriesStartKind.Started);
    }

    [Fact]
    public async Task StartTicket_AClaimThatRefuses_IsAnsweredAsRefused()
    {
        await using var rig = await Rig.BuildAsync(ClaimResult.Rejected(ClaimRejectionReason.TicketLeftInPlace, "its last attempt failed"), null);
        var added = await rig.AddAsync(requiredBy: ["1"]);

        var start = await rig.StartAsync(added);

        start.Kind.Should().Be(SeriesStartKind.Refused);
        start.Text.Should().Contain("TicketLeftInPlace").And.Contain("its last attempt failed");
    }

    [Fact]
    public async Task StartTicket_ACapacityQueue_IsAnsweredAsQueued()
    {
        await using var rig = await Rig.BuildAsync(ClaimResult.Queued("the sandbox host is full"), "run-4");
        var added = await rig.AddAsync();

        var start = await rig.StartAsync(added);

        start.Should().Be(new SeriesStart(SeriesStartKind.Queued,
            $"Ticket {added} is queued as run run-4: the sandbox host is full; it starts once there is room.", "run-4"));
    }

    // The start made it claimable, and a webhook claimed it before the poller's route did: the run that holds it is this start's.
    [Fact]
    public async Task StartTicket_ClaimedFirstByTheWebhook_IsAnsweredWithTheRunThatHoldsIt()
    {
        await using var rig = await Rig.BuildAsync(ClaimResult.AlreadyClaimed(), null, leasedAfterSpawn: "run-5");
        var added = await rig.AddAsync();

        var start = await rig.StartAsync(added);

        start.Should().Be(new SeriesStart(SeriesStartKind.Started, $"Ticket {added} started: run run-5.", "run-5"));
    }

    // A one-spec filing as Work it out files a series ticket: the approved set stored, the stamp on the ticket.
    private static AgentSmith.Application.Services.Filing.FilingRequest SeriesFiling() =>
        new("conv-1", "sample.user", [], [], new AgentSmith.Contracts.Models.TicketGraphOutcome([new AgentSmith.Contracts.Models.GraphTicket(
            "api", "Title api", "Requirement of api.", ["api works"], [], [])
        {
            Specs = [new AgentSmith.Contracts.Models.SpecDraft("api-spec", "Goal of api", "spec: api-spec\ngoal: \"Goal of api\"\n", [])],
        }]), MayStartRuns: false);

    private sealed class Rig : IAsyncDisposable
    {
        private readonly Mock<ISpawnPipelineRunsUseCase> _spawn = new();
        private RealCompositionHarness _harness = null!;
        private AsyncServiceScope _scope;
        private SeriesRunRecord _series = null!;

        public int Spawned => _spawn.Invocations.Count;

        public IServiceProvider Services => _scope.ServiceProvider;

        public Task RequireAsync(string ticketId, string requiredId, bool startsWhenMet) =>
            Services.GetRequiredService<ITicketRequirementStore>().AddAsync([new TicketRequirementEdge("github-filing", $"github-{ticketId}",
                ticketId, Project, $"github-{requiredId}", requiredId, startsWhenMet, DateTimeOffset.UtcNow, "conv-1")], default);

        private ActTracker _tracker = null!;

        /// <summary>The labels the ticket was created with and every label added since.</summary>
        public IReadOnlyList<string> Labels(string ticketId) =>
            _tracker.GetTicketAsync(new AgentSmith.Domain.Models.TicketId(ticketId), default).Result.Labels;

        public static async Task<Rig> BuildAsync(ClaimResult claim, string? runId, string? leasedAfterSpawn = null,
            string fixture = FixturePaths.Filing)
        {
            var (rig, tracker) = (new Rig(), new ActTracker());
            rig._tracker = tracker;
            var leases = new Mock<IActiveRunLease>();
            leases.Setup(l => l.GetByTicketAsync(Project, It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, AgentSmith.Domain.Models.TicketId id, CancellationToken _) => rig.Spawned > 0 && leasedAfterSpawn is not null
                    ? new StaleLease(Project, id, leasedAfterSpawn, null) : null);
            rig._spawn.Setup(s => s.ExecuteAsync(It.IsAny<AgentSmithConfig>(), It.Is<ResolvedProject>(p => p.Name == Project), It.IsAny<string>(),
                    It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(), It.IsAny<CancellationToken>(),
                    It.IsAny<Dictionary<string, string>?>()))
                .ReturnsAsync(new SpawnResult([claim], runId));
            rig._harness = RealCompositionHarness.Build(FixturePaths.For(fixture), services =>
            {
                services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new ActTrackerFactory(tracker));
                services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(new ActRunEvents());
                services.RemoveAll<ISpawnPipelineRunsUseCase>().AddSingleton(rig._spawn.Object);
                if (leasedAfterSpawn is not null) services.RemoveAll<IActiveRunLease>().AddSingleton(leases.Object);
                services.RemoveAll<IRedisClaimLock>().AddSingleton(Mock.Of<IRedisClaimLock>(l =>
                    l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()) == Task.FromResult<string?>("token")));
            });
            rig._scope = rig._harness.Services.CreateAsyncScope();
            await tracker.CreateAsync("Existing", "body", [], null, default); // "1", the series' first ticket
            rig._series = new SeriesRunRecord("sr-1", "conv-1", Project, "sample.user", true, SeriesRunStates.Running,
                [new SeriesTicket("1", "1", "Existing", false, DateTimeOffset.UtcNow)], DateTimeOffset.UtcNow);
            await rig.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(rig._series, default);
            return rig;
        }

        // add_ticket as the master's tool does it: filed through the edit act, then logged on the series as its act.
        public async Task<string> AddAsync(string[]? requires = null, string[]? requiredBy = null)
        {
            var store = Services.GetRequiredService<ISeriesRunStore>();
            var added = await Services.GetRequiredService<ISeriesEditActs>().AddTicketAsync(
                (await store.GetAsync(_series.Id, default))!, "Server prerequisite", "add the endpoint first", requires ?? [], requiredBy ?? [], default);
            added.Changed.Should().BeTrue(added.Answer);
            await store.UpdateAsync(_series.Id, r => r with
            {
                Acts = [.. r.Acts, new SeriesAct(DateTimeOffset.UtcNow, SeriesAct.AddTicketTool,
                    added.TicketId, $"added {added.Key}", "a prerequisite")],
            }, default);
            return added.TicketId!;
        }

        public Task LandAsync(string ticketId) => Services.GetRequiredService<ITicketLandingStore>().TryRecordAsync(new TicketLandingFact(
            "github-filing", $"github-{ticketId}", ticketId, Project, TicketLandingKinds.Merged, DateTimeOffset.UtcNow, null, null, null), default);

        public Task<SeriesStart> StartAsync(string ticketId) => Services.GetRequiredService<ISeriesTicketActs>().StartAsync(Project, ticketId, default);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _harness.DisposeAsync();
        }
    }
}
