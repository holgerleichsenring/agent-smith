using AgentSmith.Application.Services;
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
    private static readonly AgentSmithConfig EmptyConfig = new();
    private readonly SqliteConnection _connection;

    public CapacityQueueFunnelTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Spawn_CapacityDenied_CreatesOneQueuedRunRow_SecondPollDoesNotDuplicate()
    {
        var harness = new Harness(_connection, CapacityDecision.Deny("namespace at capacity"));

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
        var harness = new Harness(_connection, CapacityDecision.Deny("full"));
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
        var harness = new Harness(_connection, CapacityDecision.Deny("full"));
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

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private sealed class Harness
    {
        private readonly SpawnPipelineRunsUseCase _sut;
        private readonly ResolvedProject _project;
        private CapacityDecision _capacity;

        public int ClaimCount { get; private set; }
        public ClaimRequest? LastRequest { get; private set; }

        public Harness(SqliteConnection connection, CapacityDecision capacity)
        {
            _capacity = capacity;
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

            var probe = new Mock<ISandboxCapacityProbe>();
            probe.Setup(p => p.HasCapacityAsync(It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _capacity);

            _sut = new SpawnPipelineRunsUseCase(
                claimService.Object,
                CapacityTestDoubles.PassthroughResolver(),
                CapacityTestDoubles.NoOrchestrator(),
                probe.Object,
                BuildDbQueue(connection),
                NullLogger<SpawnPipelineRunsUseCase>.Instance);
        }

        public void Admit() => _capacity = CapacityDecision.Admit();

        public Task<SpawnResult> SpawnAsync(string ticketId) =>
            _sut.ExecuteAsync(
                EmptyConfig, _project, "fix-bug",
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
