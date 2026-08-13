using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrailRow = AgentSmith.Infrastructure.Persistence.Entities.RunEvent;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0378: the stream-authoritative terminal reconciler repairs a run row the
/// drain left behind — stuck 'running' with the RunFinished only in the Redis
/// stream, or terminal but missing its RunFinished trail row — exactly once.
/// </summary>
public sealed class RunTerminalReconcilerTests : IDisposable
{
    private const string RunId = "2026-07-24T10-00-00-fb2d";

    private readonly SqliteConnection _connection;

    public RunTerminalReconcilerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Reconciler_StuckRunningRow_WithTerminalStreamEvent_Repaired()
    {
        // Arrange
        await SeedRunAsync(status: "running", finishedAt: null, trailSeqs: new long[] { 0, 1, 2 });
        var reconciler = NewReconciler();

        // Act
        await reconciler.ReconcileAsync(Terminal("success"), CancellationToken.None);

        // Assert
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == RunId);
        run.Status.Should().Be("success");
        run.FinishedAt.Should().NotBeNull();
        run.Summary.Should().Be("done");
        var trail = check.Set<TrailRow>()
            .Where(e => e.RunId == RunId && e.Type == nameof(EventType.RunFinished)).ToList();
        trail.Should().ContainSingle().Which.Seq.Should().Be(3, "the trail row continues the sequence");
    }

    [Fact]
    public async Task Reconciler_TerminalRowMissingTrailRow_AppendsExactlyOne()
    {
        // Arrange: the applier committed the status but the trail flush was lost
        // (shutdown between the two transactions).
        await SeedRunAsync(status: "success", finishedAt: DateTimeOffset.UtcNow, trailSeqs: new long[] { 0 });
        var reconciler = NewReconciler();

        // Act
        await reconciler.ReconcileAsync(Terminal("success"), CancellationToken.None);

        // Assert
        using var check = new AgentSmithDbContext(Options());
        check.Set<TrailRow>().Count(e => e.RunId == RunId && e.Type == nameof(EventType.RunFinished))
            .Should().Be(1);
    }

    [Fact]
    public async Task Reconciler_FullyPersistedRun_SecondPass_LeavesRowAndTrailUntouched()
    {
        // Arrange
        await SeedRunAsync(status: "running", finishedAt: null, trailSeqs: new long[] { 0 });
        var reconciler = NewReconciler();
        await reconciler.ReconcileAsync(Terminal("failed"), CancellationToken.None);

        // Act: a second cold start reconciles the same stream terminal again.
        await reconciler.ReconcileAsync(Terminal("failed"), CancellationToken.None);

        // Assert
        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == RunId).Status.Should().Be("failed");
        check.Set<TrailRow>().Count(e => e.RunId == RunId && e.Type == nameof(EventType.RunFinished))
            .Should().Be(1);
    }

    [Fact]
    public async Task Reconciler_WaitingStatusQueued_NotTreatedAsTerminal()
    {
        // Arrange: RunFinished(status=queued) is a WAITING handoff, not an end.
        await SeedRunAsync(status: "queued", finishedAt: null, trailSeqs: Array.Empty<long>());
        var reconciler = NewReconciler();

        // Act
        await reconciler.ReconcileAsync(Terminal("queued"), CancellationToken.None);

        // Assert
        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == RunId).Status.Should().Be("queued");
        check.Set<TrailRow>().Count(e => e.RunId == RunId).Should().Be(0);
    }

    [Fact]
    public async Task Reconciler_UnknownRunRow_DoesNothing()
    {
        // Arrange
        var reconciler = NewReconciler();

        // Act
        await reconciler.ReconcileAsync(Terminal("success"), CancellationToken.None);

        // Assert
        using var check = new AgentSmithDbContext(Options());
        check.Runs.Any(r => r.Id == RunId).Should().BeFalse();
        check.Set<TrailRow>().Count(e => e.RunId == RunId).Should().Be(0);
    }

    private IRunTerminalReconciler NewReconciler()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        var provider = services.BuildServiceProvider();
        return new RunTerminalReconciler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RunEventApplier(new(), new(), new(), new(), new()), NullLogger<RunTerminalReconciler>.Instance);
    }

    private static RunFinishedEvent Terminal(string status) =>
        new(RunId, status, null, "done", DateTimeOffset.UtcNow, 0.5m);

    private async Task SeedRunAsync(string status, DateTimeOffset? finishedAt, long[] trailSeqs)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = RunId, Project = "p1", Pipeline = "fix-bug", TicketId = "42",
            Status = status, StartedAt = DateTimeOffset.UtcNow, FinishedAt = finishedAt,
        });
        foreach (var seq in trailSeqs)
            ctx.Add(new TrailRow
            {
                RunId = RunId, Seq = seq, Type = nameof(EventType.StepStarted),
                Timestamp = DateTimeOffset.UtcNow,
            });
        await ctx.SaveChangesAsync();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
