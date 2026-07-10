using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0320c: the projector-side TOCTOU backstop. The orchestrator cannot reach the
/// server DB, so its capacity rejection arrives as RunFinished status="queued" —
/// the applier upserts a QueuedTicket entry from the run row's own fields, keeps
/// the row in the active set (waiting, not finished), and a later RunStarted on
/// the same id promotes THAT row to running instead of inserting a duplicate.
/// </summary>
public sealed class CapacityQueueProjectionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-10T12:00:00Z");

    public CapacityQueueProjectionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Projector_QueuedRunFinished_UpsertsQueueEntry()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(new RunFinishedEvent(
            "run-1", "queued", null, "Waiting for capacity — quota exceeded", T));

        using var ctx = new AgentSmithDbContext(Options());
        var entry = ctx.QueuedTickets.Single();
        entry.Project.Should().Be("p1");
        entry.TicketId.Should().Be("42");
        entry.Pipeline.Should().Be("fix-bug");
        entry.Platform.Should().Be("github");
        entry.ReservedRunId.Should().Be("run-1");
        entry.InitialContextJson.Should().BeNull("a backstop entry has no envelope — the poller launches it");

        var run = ctx.Runs.Single();
        run.Status.Should().Be("queued");
        run.FinishedAt.Should().BeNull("queued is a waiting state, not a terminal one");
    }

    [Fact]
    public async Task Projector_SecondQueuedRejection_ReusesEntry_NoDuplicate()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(new RunFinishedEvent("run-1", "queued", null, "Waiting — quota", T));
        // The retry storm: the ticket is re-claimed and rejected again on the SAME id.
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(new RunFinishedEvent("run-1", "queued", null, "Waiting — quota, still", T));

        using var ctx = new AgentSmithDbContext(Options());
        ctx.QueuedTickets.Should().ContainSingle().Which.ReservedRunId.Should().Be("run-1");
        ctx.Runs.Should().ContainSingle();
    }

    [Fact]
    public async Task Projector_RunStartedOnQueuedRow_PromotesToRunning_NoSecondRow()
    {
        await ApplyAsync(Started("run-1"));
        await ApplyAsync(new RunFinishedEvent("run-1", "queued", null, "Waiting — quota", T));

        await ApplyAsync(Started("run-1"));

        using var ctx = new AgentSmithDbContext(Options());
        var run = ctx.Runs.Single();
        run.Status.Should().Be("running");
        run.FinishedAt.Should().BeNull();
        run.Summary.Should().BeNull("the waiting reason is obsolete once the run starts");
        ctx.RunRepos.Count(r => r.RunId == "run-1").Should().Be(1, "repos are never duplicated by the upsert");
    }

    private static RunStartedEvent Started(string runId) => new(
        runId, "ticket", "fix-bug", ["repo-a"], T, "claude", "42",
        Project: "p1", Platform: "github");

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(RunEvent ev) =>
        await new RunEventApplier().ApplyAsync(new AgentSmithDbContext(Options()), ev, CancellationToken.None);
}
