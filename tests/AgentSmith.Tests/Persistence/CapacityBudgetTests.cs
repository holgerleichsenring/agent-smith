using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0336: the capacity budget ledger. A run only reserves when its FULL footprint
/// fits the remaining budget, the sum of reserved footprints never exceeds the
/// budget (so a started run is guaranteed its resources for its whole life), and
/// release frees room for a waiting run. An unconfigured budget is fail-open.
/// </summary>
public sealed class CapacityBudgetTests : IDisposable
{
    private const long Gi = 1024L * 1024 * 1024;
    private readonly SqliteConnection _connection;

    public CapacityBudgetTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(DbOptions());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Reserve_WithinBudget_Succeeds_ExceedingIt_Fails()
    {
        await Record("a", 4 * Gi);
        await Record("b", 4 * Gi);
        await Record("c", 4 * Gi);
        var budget = 8 * Gi;

        (await Repo().TryReserveAsync("a", null, budget, CancellationToken.None)).Should().BeTrue();
        (await Repo().TryReserveAsync("b", null, budget, CancellationToken.None)).Should().BeTrue();
        (await Repo().TryReserveAsync("c", null, budget, CancellationToken.None))
            .Should().BeFalse("a + b already fill the 8Gi budget — c must wait");

        using var db = new AgentSmithDbContext(DbOptions());
        db.RunCapacities.Where(x => x.Reserved).Sum(x => x.TotalMemBytes)
            .Should().BeLessThanOrEqualTo(budget, "sum of reservations never exceeds the budget");
    }

    [Fact]
    public async Task Release_FreesBudget_LetsAWaitingRunReserve()
    {
        await Record("a", 4 * Gi);
        await Record("b", 4 * Gi);
        var budget = 4 * Gi;
        (await Repo().TryReserveAsync("a", null, budget, CancellationToken.None)).Should().BeTrue();
        (await Repo().TryReserveAsync("b", null, budget, CancellationToken.None)).Should().BeFalse();

        await Repo().ReleaseAsync("a", CancellationToken.None);

        (await Repo().TryReserveAsync("b", null, budget, CancellationToken.None))
            .Should().BeTrue("releasing a frees its 4Gi for b");
    }

    [Fact]
    public async Task UnconfiguredBudget_IsFailOpen_AdmitsEverything()
    {
        var budget = new DbCapacityBudget(ScopeFactory(), Options.Create(new CapacityBudgetOptions()));
        await budget.RecordAsync("huge", Footprint(1000 * Gi), CancellationToken.None);

        (await budget.TryReserveAsync("huge", CancellationToken.None))
            .Should().BeTrue("no configured budget → fail-open, never wedge a run");
    }

    [Fact]
    public async Task Record_ThenGet_RoundTripsTheFootprint()
    {
        var budget = new DbCapacityBudget(ScopeFactory(), Options.Create(new CapacityBudgetOptions()));
        await budget.RecordAsync("r1", Footprint(4 * Gi), CancellationToken.None);

        var snapshot = await budget.GetAsync("r1", CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot!.Reserved.Should().BeFalse("a recorded-but-unreserved run holds no budget yet");
        snapshot.Footprint.TotalMemBytes.Should().Be(4 * Gi);
    }

    [Fact]
    public async Task TerminalRunFinished_ReleasesReservation()
    {
        var budget = new DbCapacityBudget(ScopeFactory(), Options.Create(new CapacityBudgetOptions()));
        using (var ctx = new AgentSmithDbContext(DbOptions()))
        {
            ctx.Runs.Add(new Run
            {
                Id = "run1", Project = "p1", Pipeline = "fix-bug", TicketId = "7",
                Status = "running", StartedAt = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }
        await budget.RecordAsync("run1", Footprint(4 * Gi), CancellationToken.None);
        await budget.TryReserveAsync("run1", CancellationToken.None);

        await new RunEventApplier(budget).ApplyAsync(
            new AgentSmithDbContext(DbOptions()),
            new RunFinishedEvent("run1", "success", "https://pr", "done", DateTimeOffset.UtcNow),
            CancellationToken.None);

        (await budget.GetAsync("run1", CancellationToken.None))
            .Should().BeNull("a terminal run releases its budget reservation");
    }

    // A waiting state keeps the reservation — the run is guaranteed its footprint
    // when it resumes; only a terminal status frees it.
    [Fact]
    public async Task WaitingForInput_KeepsReservation()
    {
        var budget = new DbCapacityBudget(ScopeFactory(), Options.Create(new CapacityBudgetOptions()));
        using (var ctx = new AgentSmithDbContext(DbOptions()))
        {
            ctx.Runs.Add(new Run
            {
                Id = "run2", Project = "p1", Pipeline = "fix-bug", TicketId = "8",
                Status = "running", StartedAt = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }
        await budget.RecordAsync("run2", Footprint(4 * Gi), CancellationToken.None);
        await budget.TryReserveAsync("run2", CancellationToken.None);

        await new RunEventApplier(budget).ApplyAsync(
            new AgentSmithDbContext(DbOptions()),
            new RunFinishedEvent("run2", "waiting_for_input", null, "parked", DateTimeOffset.UtcNow),
            CancellationToken.None);

        (await budget.GetAsync("run2", CancellationToken.None))
            .Should().NotBeNull("a parked run keeps its reservation");
    }

    private static RunFootprintBreakdown Footprint(long memBytes) =>
        new([new RunFootprintPod("repo", ["default"], "img", "1", "x")],
            "1", "x", 1_000_000_000, memBytes, [], "test");

    private Task Record(string runId, long memBytes) =>
        Repo().UpsertFootprintAsync(runId, "{}", 1_000_000_000, memBytes, CancellationToken.None);

    private RunCapacityRepository Repo() => new(new AgentSmithDbContext(DbOptions()));

    private DbContextOptions<AgentSmithDbContext> DbOptions() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private IServiceScopeFactory ScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(DbOptions()));
        services.AddScoped<RunCapacityRepository>();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
