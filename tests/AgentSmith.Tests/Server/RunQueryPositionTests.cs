using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0320d: /api/runs serves queued runs with their live 1-based FIFO position —
/// ranked from QueuedTicket order at query time, matched via ReservedRunId,
/// never persisted on the run.
/// </summary>
public sealed class RunQueryPositionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ICapacityQueue _queue;

    public RunQueryPositionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options()))
            ctx.Database.Migrate();
        _queue = BuildDbQueue(_connection);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task GetRuns_QueuedRuns_CarryFifoPositions()
    {
        var first = await EnqueueAsync("1", "2026-07-10T12-00-00-0001");
        var second = await EnqueueAsync("2", "2026-07-10T12-00-01-0002");
        var third = await EnqueueAsync("3", "2026-07-10T12-00-02-0003");

        var (active, _) = await RunQueryEndpoints.BuildOverviewAsync(
            NewRepository(), _queue, CancellationToken.None);

        active.Should().HaveCount(3, "queued rows wait in the active set");
        active.Single(s => s.RunId == first).QueuePosition.Should().Be(1);
        active.Single(s => s.RunId == second).QueuePosition.Should().Be(2);
        active.Single(s => s.RunId == third).QueuePosition.Should().Be(3);
        active.Should().OnlyContain(s => s.Status == "queued");
    }

    [Fact]
    public async Task GetRuns_QueueDrains_PositionsRerank()
    {
        await EnqueueAsync("1", "2026-07-10T12-00-00-0001");
        var second = await EnqueueAsync("2", "2026-07-10T12-00-01-0002");
        await _queue.RemoveAsync("p1", "1", CancellationToken.None);

        var (active, _) = await RunQueryEndpoints.BuildOverviewAsync(
            NewRepository(), _queue, CancellationToken.None);

        active.Single(s => s.RunId == second).QueuePosition
            .Should().Be(1, "positions are computed live from the drained queue");
    }

    [Fact]
    public async Task GetRuns_RunningRun_HasNoQueuePosition()
    {
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Runs.Add(new AgentSmith.Infrastructure.Persistence.Entities.Run
            {
                Id = "run-live", Project = "p1", Pipeline = "fix-bug",
                TicketId = "9", Status = "running", StartedAt = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        var (active, _) = await RunQueryEndpoints.BuildOverviewAsync(
            NewRepository(), _queue, CancellationToken.None);

        active.Single().QueuePosition.Should().BeNull();
    }

    private Task<string> EnqueueAsync(string ticketId, string runId) =>
        _queue.EnqueueAsync(new CapacityQueueCandidate(
            "p1", ticketId, "fix-bug", "github", runId,
            "waiting for sandbox capacity", ["repo-a"],
            InitialContextJson: "{}", PlanAnswersJson: null), CancellationToken.None);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunRepository NewRepository() => new(new AgentSmithDbContext(Options()));

    private static ICapacityQueue BuildDbQueue(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        return new DbCapacityQueue(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }
}
