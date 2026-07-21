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
/// p0357: the resolved cost budget (tier + cap from ScopeRepos) reaches BOTH
/// surfaces — the run row via the applier (REST path) and the live snapshot via
/// Apply (SignalR path) — so the dashboard renders spent/cap from step 4 onward.
/// </summary>
public sealed class RunBudgetResolvedTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RunBudgetResolvedTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Applier_BudgetEvent_PersistsTierAndCapOnRow()
    {
        await SeedRunAsync("run-b1");

        await ApplyAsync(new RunBudgetResolvedEvent(
            "run-b1", "large", 45m, 15_000_000, DateTimeOffset.UtcNow));

        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-b1");
        run.BudgetTier.Should().Be("large");
        run.BudgetCapUsd.Should().Be(45m);
        run.BudgetCapTokens.Should().Be(15_000_000);
    }

    [Fact]
    public async Task Applier_LedgerOnlyStoryEvent_DoesNotClobberBudget()
    {
        await SeedRunAsync("run-b2");
        await ApplyAsync(new RunBudgetResolvedEvent(
            "run-b2", "medium", 8m, 1_500_000, DateTimeOffset.UtcNow));

        // A later story event (ledger only) must leave the budget untouched.
        await ApplyAsync(new RunStoryRecordedEvent(
            "run-b2", "[]", null, DateTimeOffset.UtcNow));

        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-b2");
        run.BudgetTier.Should().Be("medium");
        run.BudgetCapUsd.Should().Be(8m);
    }

    [Fact]
    public void Snapshot_Apply_BudgetEvent_LandsLive()
    {
        var snapshot = RunSnapshot.Empty("run-b3");

        var applied = snapshot.Apply(new RunBudgetResolvedEvent(
            "run-b3", "large", 45m, 15_000_000, DateTimeOffset.UtcNow));

        applied.BudgetTier.Should().Be("large");
        applied.BudgetCapUsd.Should().Be(45m);
        applied.BudgetCapTokens.Should().Be(15_000_000);
    }

    private async Task SeedRunAsync(string runId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "p1", Pipeline = "add-feature", TicketId = "19106",
            Status = "running", StartedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private Task ApplyAsync(RunEvent ev) =>
        new RunEventApplier().ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
