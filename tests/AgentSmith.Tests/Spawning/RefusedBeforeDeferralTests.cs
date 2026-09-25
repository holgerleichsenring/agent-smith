using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// 2026-09-21-77d6: the spawn funnel asks what the claim would refuse IMMEDIATELY before it
/// defers. Thirteen waiting runs were measured for one ticket, one a minute, with the queue
/// empty behind them: the funnel deferred, the pump claimed the head, the claim refused, the
/// pump dropped the entry as permanent, and the next poll — finding no head — minted another.
/// A ticket the claim would refuse is refused here, so there is nothing to drop and nothing to
/// duplicate. A ticket genuinely waiting for room is untouched: one waiting run, however long.
/// </summary>
public sealed class RefusedBeforeDeferralTests : IDisposable
{
    private const string Project = "p1";
    private const string Ticket = "42";
    private const string Tracker = "tracker-a";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly IServiceProvider _services;
    private readonly IUnmovedTicketStore _store;
    private readonly List<string> _recorded = [];
    private int _claims;

    public RefusedBeforeDeferralTests()
    {
        _services = BuildServiceProvider(_connection);
        _store = new DbUnmovedTicketStore(_services.GetRequiredService<IServiceScopeFactory>());
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Spawn_ATicketTheClaimWouldRefuse_IsNotDeferred()
    {
        await RecordUnmovedAsync();

        await SpawnAsync();

        (await Queue().CountAsync(CancellationToken.None)).Should().Be(0,
            "a ticket the claim refuses has nothing to wait for — queuing it only gives the "
            + "pump a head to drop and the next poll a reason to mint a second waiting run");
        _claims.Should().Be(0, "the refusal is answered without attempting the claim");
    }

    [Fact]
    public async Task Spawn_ATicketTheClaimWouldRefuse_ReportsTheRefusalRatherThanQueued()
    {
        await RecordUnmovedAsync();

        var result = await SpawnAsync();

        var claim = result.ClaimResults.Should().ContainSingle().Subject;
        claim.Outcome.Should().Be(ClaimOutcome.Rejected,
            "a queued outcome would tell the poller the ticket is waiting for room it is not waiting for");
        claim.Rejection.Should().Be(ClaimRejectionReason.TicketLastLeftUnmoved);
        claim.Error.Should().Contain(Tracker).And.Contain(Project,
            "the operator is given the same reason the claim would have given");
    }

    [Fact]
    public async Task Spawn_ATicketTheClaimWouldRefuse_WritesNoQueueEntryAndNoRunRow()
    {
        await RecordUnmovedAsync();

        await SpawnAsync();

        using var ctx = Context();
        ctx.QueuedTickets.Should().BeEmpty();
        ctx.Runs.Should().BeEmpty("no run was minted, so the operator's surface shows none");
    }

    [Fact]
    public async Task Spawn_ATicketWithNoStandingRefusal_IsStillDeferredWhenThereIsNoRoom()
    {
        var result = await SpawnAsync();

        result.ClaimResults.Should().ContainSingle().Which.Outcome.Should().Be(ClaimOutcome.Queued);
        using var ctx = Context();
        ctx.QueuedTickets.Single().TicketId.Should().Be(Ticket);
        ctx.Runs.Single().Status.Should().Be("queued");
    }

    [Fact]
    public async Task Spawn_ATicketWaitingForRoomAcrossTwoPolls_KeepsOneReservedRun()
    {
        await SpawnAsync();
        await SpawnAsync();

        using var ctx = Context();
        var entry = ctx.QueuedTickets.Single();
        ctx.Runs.Single().Id.Should().Be(entry.ReservedRunId);
        _recorded.Should().OnlyContain(id => id == entry.ReservedRunId,
            "the second poll recognises the head and reuses its reserved run id rather than "
            + "minting a second one");
    }

    [Fact]
    public async Task Spawn_AnUnknownPipeline_IsRefusedRatherThanQueued()
    {
        var result = await SpawnAsync(pipelineName: "no-such-pipeline");

        result.ClaimResults.Should().ContainSingle()
            .Which.Rejection.Should().Be(ClaimRejectionReason.UnknownPipeline);
        using var ctx = Context();
        ctx.QueuedTickets.Should().BeEmpty("the claim would refuse it every time it is retried");
    }

    // The claim service keeps asking the same two refusals in the same order through the shared
    // seam. The order is load-bearing: the unmoved gate reads config.Projects BY NAME, so an
    // unknown project refused second would throw instead of being rejected.
    [Fact]
    public async Task Claim_TheRefusalOrderAndOutcomes_AreUnchanged()
    {
        await _store.RecordAsync(
            new UnmovedTicketFact("ghost", Ticket, Tracker, "Failed",
                TicketFinalizeOutcome.TrackerRejectedTheStatus), CancellationToken.None);
        await RecordUnmovedAsync();

        var unknownProject = await ClaimAsync(Request("ghost", "code"));
        var unknownPipeline = await ClaimAsync(Request(Project, "no-such-pipeline"));
        var unmoved = await ClaimAsync(Request(Project, "code"));

        unknownProject.Rejection.Should().Be(ClaimRejectionReason.UnknownProject,
            "the config pre-check runs first, before the gate reads the project by name");
        unknownPipeline.Rejection.Should().Be(ClaimRejectionReason.UnknownPipeline);
        unmoved.Rejection.Should().Be(ClaimRejectionReason.TicketLastLeftUnmoved);
    }

    private Task<ClaimResult> ClaimAsync(ClaimRequest request) =>
        ClaimService().ClaimAsync(request, Config(), CancellationToken.None);

    private Task<SpawnResult> SpawnAsync(string pipelineName = "code") =>
        Funnel().ExecuteAsync(
            Config(), Config().Projects[Project], pipelineName,
            new IncomingTicketEnvelope { TicketId = Ticket, Platform = "github" },
            new WebhookTriggerConfig { DefaultPipeline = "code" },
            CancellationToken.None);

    private Task RecordUnmovedAsync() => _store.RecordAsync(
        new UnmovedTicketFact(Project, Ticket, Tracker, "Failed",
            TicketFinalizeOutcome.TrackerRejectedTheStatus),
        CancellationToken.None);

    private SpawnPipelineRunsUseCase Funnel() => new(
        CountingClaimService(),
        CapacityTestDoubles.StubCalculator(),
        NeverReserve(),
        Queue(),
        CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.NoHolds(),
        CapacityTestDoubles.AlwaysAdmit(),
        ApprovedSetDoubles.Carrier(),
        CapacityTestDoubles.NoNudge(),
        _store,
        NullLogger<SpawnPipelineRunsUseCase>.Instance);

    private ITicketClaimService CountingClaimService()
    {
        var service = new Mock<ITicketClaimService>();
        service.Setup(c => c.ClaimAsync(
                It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
            .Callback(() => _claims++)
            .ReturnsAsync(ClaimResult.Claimed());
        return service.Object;
    }

    // The room is full, which is what makes the funnel reach its deferral branch at all.
    private ICapacityBudget NeverReserve()
    {
        var budget = new Mock<ICapacityBudget>();
        budget.Setup(b => b.RecordAsync(
                It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()))
            .Callback<string, RunFootprintBreakdown, CancellationToken>((id, _, _) => _recorded.Add(id))
            .Returns(Task.CompletedTask);
        budget.Setup(b => b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        budget.Setup(b => b.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return budget.Object;
    }

    private TicketClaimService ClaimService()
    {
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        var transitioner = new Mock<ITicketStatusTransitioner>();
        transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(), It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Succeeded());
        var factory = new Mock<ITicketStatusTransitionerFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(transitioner.Object);
        return new TicketClaimService(
            claimLock.Object, _store, factory.Object, new Mock<IRedisJobQueue>().Object,
            new NoOpActiveRunLease(), new InMemoryTakenTicketStore(),
            NullLogger<TicketClaimService>.Instance);
    }

    private ICapacityQueue Queue() =>
        new DbCapacityQueue(_services.GetRequiredService<IServiceScopeFactory>());

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            [Project] = new()
            {
                Name = Project,
                Repos = [new RepoConnection { Name = "repo-a" }],
                Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code", TriggerStatuses = ["Approved"], DoneStatus = "closed",
                },
            },
        },
    };

    private static ClaimRequest Request(string project, string pipeline) =>
        new("GitHub", project, new TicketId(Ticket), pipeline);

    private static IServiceProvider BuildServiceProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        services.AddScoped<RunRepository>();
        services.AddScoped<UnmovedTicketRepository>();
        return services.BuildServiceProvider();
    }
}
