using AgentSmith.Tests.TestSupport;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services.Init;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// p0320c: the spawn funnel against a REAL SQLite-backed capacity queue. A
/// capacity-denied ticket produces exactly ONE queue entry and ONE visible
/// "queued" Run row no matter how often the poll retries, arrival order is
/// strict FIFO (an admitted newcomer never overtakes the head), and the head
/// launches with its reserved run id, deleting its entry.
/// </summary>
public sealed class CapacityQueueFunnelTests : IDisposable
{
    // 2026-09-21-77d6: the funnel asks what the claim would refuse before it defers, and both
    // refusals read config.Projects by name — a deferral test's config carries the project.
    private static readonly AgentSmithConfig ClaimableConfig = new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p1"] = new()
            {
                Name = "p1",
                Tracker = new TrackerConnection { Name = "tracker-a", Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "fix-bug" },
            },
        },
    };
    private readonly SqliteConnection _connection;

    public CapacityQueueFunnelTests()
    {
        _connection = MigratedStoreTemplate.OpenCopy();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Spawn_CapacityDenied_CreatesOneQueuedRunRow_SecondPollDoesNotDuplicate()
    {
        var harness = new Harness(_connection, fits: false);

        var first = await harness.SpawnAsync(ticketId: "42");
        var second = await harness.SpawnAsync(ticketId: "42");

        first.ClaimResults.Single().Outcome.Should().Be(ClaimOutcome.Queued);
        second.ClaimResults.Single().Outcome.Should().Be(ClaimOutcome.Queued);
        harness.ClaimCount.Should().Be(0, "a deferred ticket must not be claimed");

        using var ctx = new AgentSmithDbContext(Options());
        var entry = ctx.QueuedTickets.Single();
        entry.TicketId.Should().Be("42");
        var run = ctx.Runs.Single();
        run.Status.Should().Be("queued");
        run.Id.Should().Be(entry.ReservedRunId, "the entry reserves its single visible run row");
        run.FinishedAt.Should().BeNull("a queued row waits in the active set");
    }

    [Fact]
    public async Task Spawn_QueueNonEmpty_NewTicketEnqueuedBehindHead_NoOvertaking()
    {
        // Head "1" arrived while capacity was full; then room appears.
        var harness = new Harness(_connection, fits: false);
        await harness.SpawnAsync(ticketId: "1");
        harness.Admit();

        var result = await harness.SpawnAsync(ticketId: "2");

        result.ClaimResults.Single().Outcome.Should().Be(ClaimOutcome.Queued);
        harness.ClaimCount.Should().Be(0, "strict FIFO: a fitting run never overtakes the head");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.QueuedTickets.OrderBy(q => q.Id).Select(q => q.TicketId)
            .Should().Equal("1", "2");
    }

    [Fact]
    public async Task Spawn_HeadAdmitted_ClaimsWithReservedRunId_AndRemovesEntry()
    {
        var harness = new Harness(_connection, fits: false);
        await harness.SpawnAsync(ticketId: "1");
        string reserved;
        using (var ctx = new AgentSmithDbContext(Options()))
            reserved = ctx.QueuedTickets.Single().ReservedRunId!;
        harness.Admit();

        var result = await harness.SpawnAsync(ticketId: "1");

        result.ClaimResults.Single().Outcome.Should().Be(ClaimOutcome.Claimed);
        harness.LastRequest!.ExistingRunId.Should().Be(reserved);
        using var check = new AgentSmithDbContext(Options());
        check.QueuedTickets.Should().BeEmpty("the launched head leaves the queue");
    }

    // ---- 2026-09-21-5c17: a waiting run names the gate that refused it ----

    [Fact]
    public async Task Admission_TheSandboxProbeRefuses_TheRunWaitsWithTheBoundAndUsageItNamed()
    {
        // The REAL Docker probe, because the sentence that names the bound is written there,
        // and a budget that fits — so the only gate that can have refused is the probe.
        var harness = new Harness(_connection, fits: true, probe: DockerProbeAtBound(bound: 1, running: 2));

        await harness.SpawnAsync(ticketId: "42");

        using var ctx = new AgentSmithDbContext(Options());
        var waiting = ctx.Runs.Single().Summary;
        waiting.Should().StartWith("2 running +", "the row clips its tail, so the usage leads");
        waiting.Should().Contain("concurrent-sandbox cap of 1");
        waiting.Should().NotContain("budget", "no budget refused this run");
    }

    [Fact]
    public async Task Admission_TheLedgerRefuses_TheRunWaitsWithADifferentSentenceThanTheProbeGives()
    {
        var byLedger = new Harness(_connection, fits: false);
        await byLedger.SpawnAsync(ticketId: "42");
        string ledgerSaid;
        using (var ctx = new AgentSmithDbContext(Options()))
            ledgerSaid = ctx.Runs.Single().Summary!;

        using var second = MigratedStoreTemplate.OpenCopy();
        var byProbe = new Harness(second, fits: true, probe: DockerProbeAtBound(bound: 1, running: 2));
        await byProbe.SpawnAsync(ticketId: "42");
        using var other = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(second).Options);

        ledgerSaid.Should().Be(CapacityReasons.LedgerFull(StubFootprint()));
        ledgerSaid.Should().NotBe(other.Runs.Single().Summary,
            "an operator has to be able to tell which of the two gates is holding the run");
    }

    [Fact]
    public async Task Admission_AProbeThatRefusesWithNoWords_StillProducesANonEmptyReason()
    {
        // The decision type allows a denial with no reason and the surface renders an empty
        // element for an empty string — worse for an operator than a wrong sentence.
        var harness = new Harness(_connection, fits: true, probe: CapacityTestDoubles.AlwaysDeny(reason: "   "));

        await harness.SpawnAsync(ticketId: "42");

        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Single().Summary.Should().NotBeNullOrWhiteSpace();
        ctx.QueuedTickets.Single().Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Spawn_TheProbesOwnSentence_IsWhatReachesTheRunRowOnTheFirstEnqueue()
    {
        // The row already carried A sentence before this phase, so this asserts WHICH one.
        const string itsOwnWords = "4 running + 1 needed exceeds the concurrent-sandbox cap of 4 — waiting.";
        var harness = new Harness(_connection, fits: true, probe: CapacityTestDoubles.AlwaysDeny(itsOwnWords));

        await harness.SpawnAsync(ticketId: "42");

        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Single().Summary.Should().Be(itsOwnWords);
        ctx.QueuedTickets.Single().Reason.Should().Be(itsOwnWords);
    }

    [Fact]
    public async Task Spawn_ARunBehindAQueueHead_AttemptsNoReservationAndKeepsItsPositionSentence()
    {
        var harness = new Harness(_connection, fits: false);
        await harness.SpawnAsync(ticketId: "1");
        harness.ForgetCalls();

        await harness.SpawnAsync(ticketId: "2");

        harness.ProbeCalls.Should().Be(0, "a run behind the head triggers no pod listing");
        harness.ReapCalls.Should().Be(0, "nor a corpse reap");
        harness.ReserveCalls.Should().Be(0, "nor a reservation");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Single(r => r.TicketId == "2").Summary.Should().Be("waiting in line behind p1/#1");
    }

    [Fact]
    public async Task Spawn_ARunThatIsAdmitted_IsUnaffected()
    {
        var harness = new Harness(_connection, fits: true);

        var result = await harness.SpawnAsync(ticketId: "42");

        result.ClaimResults.Single().Outcome.Should().Be(ClaimOutcome.Claimed);
        harness.ClaimCount.Should().Be(1);
        using var ctx = new AgentSmithDbContext(Options());
        ctx.QueuedTickets.Should().BeEmpty("an admitted run never reaches the queue");
    }

    [Fact]
    public async Task InitAdmission_UsesTheSameLedgerSentenceAsTheFunnel()
    {
        var funnel = new Harness(_connection, fits: false);
        await funnel.SpawnAsync(ticketId: "42");

        var refused = await NewInitAdmission(fits: false)
            .TryAdmitAsync(new ResolvedProject { Name = "p1" }, "fix-bug", "run-1", CancellationToken.None);

        using var ctx = new AgentSmithDbContext(Options());
        refused.Admitted.Should().BeFalse();
        refused.Reason.Should().Be(ctx.Runs.Single().Summary,
            "one door's ledger refusal reads exactly like the other's");
    }

    private static RunFootprintBreakdown StubFootprint() =>
        new([], "1", "4Gi", 1_000_000_000, 4L * 1024 * 1024 * 1024, [], "stub footprint");

    private static InitRunAdmission NewInitAdmission(bool fits)
    {
        var budget = new Mock<ICapacityBudget>();
        budget.Setup(b => b.RecordAsync(
                It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        budget.Setup(b => b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fits);
        budget.Setup(b => b.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new InitRunAdmission(
            CapacityTestDoubles.StubCalculator(), budget.Object, CapacityTestDoubles.NoHolds(),
            CapacityTestDoubles.AlwaysAdmit(), NullLogger<InitRunAdmission>.Instance);
    }

    // The real Docker probe over a daemon already at its bound, with the bound named in the
    // catalog the probe resolves through — so the refusal carries the resolved number.
    private static ISandboxCapacityProbe DockerProbeAtBound(int bound, int running)
    {
        var containers = Enumerable.Range(0, running)
            .Select(i => new ContainerListResponse
            {
                ID = $"c{i}",
                Labels = new Dictionary<string, string> { [DockerContainerSpecBuilder.JobIdLabel] = $"job{i}" },
            })
            .ToList();
        var ops = new Mock<IContainerOperations>();
        ops.Setup(c => c.ListContainersAsync(
                It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<ContainerListResponse>)containers);
        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(ops.Object);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(
            new AgentSmithConfig { Sandbox = new SandboxGlobalConfig { MaxConcurrentSandboxes = bound } });
        return new DockerCapacityProbe(
            docker.Object, new DockerSandboxQuery(new SandboxOwnerIdentity("store-0123456789abcdef")),
            loader.Object, new ServerContext("agentsmith.yml"), NullLogger<DockerCapacityProbe>.Instance);
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private sealed class Harness
    {
        private readonly SpawnPipelineRunsUseCase _sut;
        private readonly ResolvedProject _project;
        private bool _fits;

        public int ClaimCount { get; private set; }
        public ClaimRequest? LastRequest { get; private set; }

        // 2026-09-21-5c17: the three things a run behind the queue head must not pay for.
        public int ProbeCalls { get; private set; }
        public int ReapCalls { get; private set; }
        public int ReserveCalls { get; private set; }

        public Harness(SqliteConnection connection, bool fits, ISandboxCapacityProbe? probe = null)
        {
            _fits = fits;
            _project = new ResolvedProject
            {
                Name = "p1",
                Repos = [new RepoConnection { Name = "repo-a" }],
            };

            var claimService = new Mock<ITicketClaimService>();
            claimService.Setup(c => c.ClaimAsync(
                    It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
                .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>(
                    (r, _, _) => { ClaimCount++; LastRequest = r; })
                .ReturnsAsync(ClaimResult.Claimed());

            var budget = new Mock<ICapacityBudget>();
            budget.Setup(b => b.RecordAsync(
                    It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            budget.Setup(b => b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => { ReserveCalls++; return _fits; });
            budget.Setup(b => b.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var counted = new Mock<ISandboxCapacityProbe>();
            counted.Setup(pr => pr.HasCapacityAsync(It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()))
                .Returns((RunFootprint f, CancellationToken c) =>
                {
                    ProbeCalls++;
                    return (probe ?? CapacityTestDoubles.AlwaysAdmit()).HasCapacityAsync(f, c);
                });
            var reaper = new Mock<ISandboxCorpseReaper>();
            reaper.Setup(r => r.ReapCorpsesAsync(It.IsAny<CancellationToken>()))
                .Returns(() => { ReapCalls++; return Task.FromResult(0); });

            _sut = new SpawnPipelineRunsUseCase(
                claimService.Object,
                CapacityTestDoubles.StubCalculator(),
                budget.Object,
                BuildDbQueue(connection),
                reaper.Object, CapacityTestDoubles.NoHolds(), counted.Object,
                TestSupport.ApprovedSetDoubles.Carrier(),
                CapacityTestDoubles.NoNudge(),
                CapacityTestDoubles.NoStandingRefusal(),
                NullLogger<SpawnPipelineRunsUseCase>.Instance);
        }

        public void Admit() => _fits = true;

        public void ForgetCalls() { ProbeCalls = 0; ReapCalls = 0; ReserveCalls = 0; }

        public Task<SpawnResult> SpawnAsync(string ticketId) =>
            _sut.ExecuteAsync(
                ClaimableConfig, _project, "fix-bug",
                new IncomingTicketEnvelope { TicketId = ticketId, Platform = "github" },
                new WebhookTriggerConfig { DoneStatus = "closed" },
                CancellationToken.None);
    }

    private static ICapacityQueue BuildDbQueue(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        var provider = services.BuildServiceProvider();
        return new DbCapacityQueue(provider.GetRequiredService<IServiceScopeFactory>());
    }
}
