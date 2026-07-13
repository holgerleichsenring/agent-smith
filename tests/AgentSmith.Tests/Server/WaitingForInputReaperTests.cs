using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0327: Reapers_WaitingForInput_NotReaped. A parked run is structurally
/// reaper-safe BY the checkpoint contract: the lease is RELEASED at checkpoint
/// (re-claimed at resume with the reserved run id), the sandboxes are disposed,
/// and the row keeps FinishedAt null — so ActiveRunReaper (leases), the
/// CancelEnforcer candidate query (CancelRequested), and the queued-cancel
/// endpoint gate (status=="queued") all leave it alone for days.
/// </summary>
public sealed class WaitingForInputReaperTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-11T12:00:00Z");

    public WaitingForInputReaperTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Reapers_WaitingForInput_NotReaped()
    {
        // The checkpointed run's lifecycle: lease claimed at spawn, RELEASED on
        // park (ExecutePipelineUseCase's finally); the row stays active.
        var lease = new DbActiveRunLease(ScopeFactory());
        await lease.TryClaimAsync("p1", new TicketId("42"), CancellationToken.None);
        await lease.AttachRunAsync("p1", new TicketId("42"), "run-1", null, CancellationToken.None);
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(new RunFinishedEvent("run-1", "waiting_for_input", null, "Waiting", T));
        await lease.ReleaseAsync("p1", new TicketId("42"), CancellationToken.None);

        // Days pass. ActiveRunReaper scans with a zero threshold (everything
        // held would be stale) — a parked run holds NOTHING to reap.
        var events = new Mock<IEventPublisher>(MockBehavior.Strict);
        var reaper = new ActiveRunReaper(
            lease, new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance),
            events.Object, TimeProvider.System, NullLogger<ActiveRunReaper>.Instance);
        var released = await reaper.RunOnceAsync(TimeSpan.Zero, CancellationToken.None);

        released.Should().Be(0, "the parked run holds no lease — nothing is stale");
        events.VerifyNoOtherCalls();

        using var ctx = new AgentSmithDbContext(Options());
        var run = ctx.Runs.Single();
        run.Status.Should().Be("waiting_for_input");
        run.FinishedAt.Should().BeNull();

        // The cancel enforcer's candidate query ignores it too (no cancel flag).
        var candidates = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetCancelEnforcementCandidatesAsync(T.AddDays(30), CancellationToken.None);
        candidates.Should().BeEmpty();
    }

    private IServiceScopeFactory ScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUniqueViolationTranslator>(
            new AgentSmith.Infrastructure.Persistence.Services.Translators.SqliteUniqueViolationTranslator());
        services.AddScoped<ActiveRunRepository>();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static RunStartedEvent Started(string runId) => new(
        runId, "ticket", "fix-bug", ["repo-a"], T, "claude", "42",
        Project: "p1", Platform: "github");

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(RunEvent ev) =>
        await new RunEventApplier().ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);
}
