using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>2026-11-27-3532a: the boundaries of <see cref="SeriesDirectStartCompositionTests"/> — the tracker, the claim with the
/// lease it writes (a leased ticket is AlreadyClaimed, as the lease insert answers), and the sandbox host's room. Everything
/// between a trigger or a master's act and the claim is the real composition.</summary>
internal sealed class SeriesDirectStartRig : IAsyncDisposable
{
    public const string Project = "fixture-file";
    private static readonly DateTimeOffset Noon = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<string, string> _leases = [];
    private RealCompositionHarness _harness = null!;
    private AsyncServiceScope _scope;

    public ActTracker Tracker { get; } = new();
    public ActRunEvents Events { get; } = new();
    public List<ClaimRequest> Claims { get; } = [];
    public IServiceProvider Services => _scope.ServiceProvider;
    public RealCompositionHarness Harness => _harness;

    public static Task<SeriesDirectStartRig> BuildAsync(bool room = true)
    {
        var rig = new SeriesDirectStartRig();
        rig._harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services => rig.Bind(services, room));
        rig._scope = rig._harness.Services.CreateAsyncScope();
        return Task.FromResult(rig);
    }

    private void Bind(IServiceCollection services, bool room)
    {
        services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new ActTrackerFactory(Tracker));
        services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(Events);
        services.RemoveAll<ITicketClaimService>().AddSingleton(Claim());
        services.RemoveAll<IActiveRunLease>().AddSingleton(Leases());
        services.RemoveAll<IRunFootprintCalculator>().AddSingleton(AgentSmith.Tests.Spawning.CapacityTestDoubles.StubCalculator());
        services.RemoveAll<ICapacityBudget>().AddSingleton(Mock.Of<ICapacityBudget>(b =>
            b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(true)));
        services.RemoveAll<ISandboxCapacityProbe>().AddSingleton(Mock.Of<ISandboxCapacityProbe>(p => p.HasCapacityAsync(
            It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()) == Task.FromResult(
                room ? CapacityDecision.Admit() : CapacityDecision.Deny("namespace quota full"))));
        services.RemoveAll<ISandboxCorpseReaper>().AddSingleton<ISandboxCorpseReaper>(new NoOpSandboxCorpseReaper());
        services.RemoveAll<IRedisClaimLock>().AddSingleton(Mock.Of<IRedisClaimLock>(l =>
            l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()) == Task.FromResult<string?>("token")));
    }

    // The claim boundary: a ticket its lease holds is AlreadyClaimed; any other is claimed and leased to the request's run.
    private ITicketClaimService Claim()
    {
        var claims = new Mock<ITicketClaimService>();
        claims.Setup(c => c.ClaimAsync(It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimRequest r, AgentSmithConfig _, CancellationToken _) =>
            {
                lock (_leases)
                {
                    if (_leases.ContainsKey(r.TicketId.Value)) return ClaimResult.AlreadyClaimed();
                    _leases[r.TicketId.Value] = r.ExistingRunId ?? "run-new";
                    Claims.Add(r);
                    return ClaimResult.Claimed();
                }
            });
        return claims.Object;
    }

    private IActiveRunLease Leases()
    {
        var leases = new Mock<IActiveRunLease>();
        leases.Setup(l => l.GetByTicketAsync(It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string project, TicketId id, CancellationToken _) =>
            {
                lock (_leases) return _leases.TryGetValue(id.Value, out var run) ? new StaleLease(project, id, run, null) : null;
            });
        return leases.Object;
    }

    /// <summary>Creates the next ticket in status "open"; a routed one carries the project's tag, so the poller's route names it.</summary>
    public Task TicketAsync(bool routed) => Tracker.CreateAsync("A ticket", "body", routed ? [Project] : [], null, default);

    /// <summary>A running series over a new ticket with the given id, in conversation conv-1, approved by sample.user.</summary>
    public async Task<SeriesRunRecord> SeriesAsync(string ticketId)
    {
        await TicketAsync(routed: false);
        var series = new SeriesRunRecord("sr-1", "conv-1", Project, "sample.user", true, SeriesRunStates.Running,
            [new SeriesTicket(ticketId, ticketId, "A ticket", false, Noon)], Noon);
        await Services.GetRequiredService<ISeriesRunStore>().OpenAsync(series, default);
        return series;
    }

    /// <summary>The route the poller takes for a ticket in a trigger status: its envelope by labels, the project's trigger, the funnel.</summary>
    public async Task<SpawnResult> TriggerAsync(string ticketId)
    {
        var project = Services.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(Services.GetRequiredService<ServerContext>().ConfigPath).Projects[Project];
        var (spawned, nowhere) = await Services.GetRequiredService<ReworkFallbackSpawn>().SpawnAsync(project, ticketId, default);
        return spawned ?? throw new InvalidOperationException($"the trigger routed nowhere: {nowhere}");
    }

    /// <summary>A filing's membership joining the ticket into the series, as the 'file' run writes it.</summary>
    public Task JoinAsync(string seriesId, string ticketId)
    {
        var pipeline = new AgentSmith.Contracts.Commands.PipelineContext();
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.SeriesRunId, seriesId);
        var project = Services.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(Services.GetRequiredService<ServerContext>().ConfigPath).Projects[Project];
        return Services.GetRequiredService<AgentSmith.Application.Services.Series.SeriesMembership>().WriteAsync(pipeline,
            new AgentSmith.Application.Services.Filing.FilingRequest("conv-1", "sample.user", [], [], null, true), project,
            [new AgentSmith.Application.Services.Filing.FiledTicket($"https://tracker.test/{ticketId}", "A ticket") { TicketId = ticketId, Key = ticketId }],
            default);
    }

    public Task<SeriesStart> StartTicketAsync(string ticketId) => Services.GetRequiredService<ISeriesTicketActs>().StartAsync(Project, ticketId, default);

    public async Task<SeriesRunRecord?> HolderAsync(string ticketId) =>
        (await Services.GetRequiredService<ISeriesRunStore>().ListAsync([Project], default))
            .FirstOrDefault(r => r.IsActive && r.Tickets.Any(t => t.TicketId == ticketId));

    public static TicketGraphOutcome List() => new([Graph("api", []), Graph("ui", ["api"])]);

    private static GraphTicket Graph(string key, IReadOnlyList<string> requires) =>
        new(key, $"Title {key}", $"Requirement of {key}.", [$"{key} works"], requires, [])
        {
            Specs = [new SpecDraft($"{key}-spec", $"Goal of {key}", $"spec: {key}-spec\ngoal: \"Goal of {key}\"\n", [])],
        };

    public static ConversationState Conversation() => new()
    {
        JobId = "s-direct", ChannelId = "d-direct", ThreadId = "d-direct", UserId = "sample.user", Platform = "dashboard",
        Project = Project, TicketId = string.Empty, StartedAt = Noon,
        Transcript =
        [
            new TranscriptTurn(TranscriptRole.User, "Split it into api and ui.", Noon, Author: "sample.user"),
            new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(1), Decision: SpecDialogDecision.Approved,
                DecisionProposalId: "prop-1"),
        ],
    };

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _harness.DisposeAsync();
    }
}
