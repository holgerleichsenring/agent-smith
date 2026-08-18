using AgentSmith.Tests.TestSupport;
using AgentSmith.Contracts.Events;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0413: the shape that decided the process reaches BOTH surfaces — the run row
/// via the applier (REST path) and the live snapshot via Apply (SignalR path) —
/// so an operator can see why a ticket got the process it got, while it runs and
/// long after. A run with no stated shape shows none, never a default one.
/// </summary>
public sealed class RunWorkShapeResolvedTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RunWorkShapeResolvedTests()
    {
        _connection = MigratedStoreTemplate.OpenCopy();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Applier_ShapeEvent_PersistsShapeAndReasonOnRow()
    {
        await SeedRunAsync("run-s1");

        await ApplyAsync(new RunWorkShapeResolvedEvent(
            "run-s1", "deterministic", "one declared set applied the same way twice",
            DateTimeOffset.UtcNow));

        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-s1");
        run.WorkShape.Should().Be("deterministic");
        run.WorkShapeReason.Should().Be("one declared set applied the same way twice");
    }

    [Fact]
    public async Task Applier_ShapeAndBudget_AreIndependentFacts()
    {
        // The tier can be Unknown (no budget event) while the shape is stated, and
        // the other way round — one must never clobber or gate the other.
        await SeedRunAsync("run-s2");
        await ApplyAsync(new RunWorkShapeResolvedEvent(
            "run-s2", "mixed", null, DateTimeOffset.UtcNow));
        await ApplyAsync(new RunBudgetResolvedEvent(
            "run-s2", "medium", 8m, 1_500_000, DateTimeOffset.UtcNow));

        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-s2");
        run.WorkShape.Should().Be("mixed");
        run.WorkShapeReason.Should().BeNull();
        run.BudgetTier.Should().Be("medium");
    }

    [Fact]
    public async Task RunView_ShowsTheShapeThatDecidedTheProcess()
    {
        await SeedRunAsync("run-s3");
        await ApplyAsync(new RunWorkShapeResolvedEvent(
            "run-s3", "judgement", "the failure has to be diagnosed first", DateTimeOffset.UtcNow));

        using var check = new AgentSmithDbContext(Options());
        var snapshot = RunSnapshotMapper.ToSnapshot(
            check.Runs.Single(r => r.Id == "run-s3"), includeStory: true);

        snapshot.WorkShape.Should().Be("judgement");
        snapshot.WorkShapeReason.Should().Be("the failure has to be diagnosed first");
    }

    [Fact]
    public async Task RunView_NoShapeStated_ShowsNone()
    {
        await SeedRunAsync("run-s4");

        using var check = new AgentSmithDbContext(Options());
        var snapshot = RunSnapshotMapper.ToSnapshot(
            check.Runs.Single(r => r.Id == "run-s4"));

        snapshot.WorkShape.Should().BeNull("an unclassified run must not claim a shape");
    }

    [Fact]
    public void Snapshot_Apply_ShapeEvent_LandsLive()
    {
        var snapshot = RunSnapshot.Empty("run-s5");

        var applied = snapshot.Apply(new RunWorkShapeResolvedEvent(
            "run-s5", "deterministic", "mechanical once the set is known", DateTimeOffset.UtcNow));

        applied.WorkShape.Should().Be("deterministic");
        applied.WorkShapeReason.Should().Be("mechanical once the set is known");
    }

    private async Task SeedRunAsync(string runId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "code", TicketId = "19106",
            Status = "running", StartedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private Task ApplyAsync(RunEvent ev) =>
        new RunEventApplier(new(), new(), new(), new(), new(), new(), new())
            .ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
