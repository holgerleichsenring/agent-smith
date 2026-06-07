using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246c: the server-side projector persists every structured RunEvent into the
/// relational store (batched trail), and the run history reads back from the DB —
/// surviving a process restart (a fresh DbContext over the same database). Proven
/// on a real SQLite engine.
/// </summary>
public sealed class RunDbProjectorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MutableTimeProvider _clock = new() { Now = DateTimeOffset.Parse("2026-06-07T12:00:00Z") };

    public RunDbProjectorTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private Factory NewFactory() => new(_connection);
    private RunDbProjector NewProjector() => new(NewFactory(), new RunEventApplier());
    private DbRunStore NewStore() => new(NewFactory());

    private static IReadOnlyList<RunEvent> SampleStream(string runId, DateTimeOffset t) => new RunEvent[]
    {
        new RunStartedEvent(runId, "ticket", "fix-bug", new[] { "primary" }, t, "claude", "42"),
        new TicketFetchedEvent(runId, "42", "Fix the bug", "desc", "Open", Array.Empty<string>(), 0, "github", t),
        new StepStartedEvent(runId, 0, "LoadCatalog", 14, t),
        new LlmCallFinishedEvent(runId, "gpt-4.1", "coding-agent", 1000, 200, 0.05m, 1200, t, "implementation", "primary"),
        new SandboxCreatedEvent(runId, "primary", "dotnet:8", "csharp", t),
        new StepFinishedEvent(runId, 0, "ok", 800, t),
        new PullRequestOutcomeEvent(runId, "primary", "opened", t, "https://pr/1"),
        new RunFinishedEvent(runId, "success", "https://pr/1", "Fixed the bug", t, 0.05m),
    };

    [Fact]
    public async Task Projector_RunEventStream_PersistsRunWithChildren_ReadBackSurvivesRestart()
    {
        var projector = NewProjector();
        foreach (var ev in SampleStream("run-1", _clock.Now))
            await projector.ProjectAsync(ev, CancellationToken.None);

        // Fresh store + fresh context = a process restart against the same DB.
        var run = await NewStore().GetRunDetailAsync("run-1", CancellationToken.None);

        run.Should().NotBeNull();
        run!.Pipeline.Should().Be("fix-bug");
        run.Status.Should().Be("success");
        run.Summary.Should().Be("Fixed the bug");
        run.TicketTitle.Should().Be("Fix the bug");
        run.CostTotalUsd.Should().Be(0.05m);
        run.Steps.Should().ContainSingle(s => s.StepName == "LoadCatalog" && s.Status == "ok");
        run.LlmCalls.Should().ContainSingle(l => l.Model == "gpt-4.1");
        run.Sandboxes.Should().ContainSingle(s => s.RepoName == "primary");
        run.Repos.Should().ContainSingle(r => r.RepoName == "primary" && r.PrStatus == "opened" && r.PrUrl == "https://pr/1");
    }

    [Fact]
    public async Task Projector_BatchesTrail_FlushesEveryStructuredEventOnRunFinished()
    {
        var stream = SampleStream("run-1", _clock.Now);
        var projector = NewProjector();
        foreach (var ev in stream) await projector.ProjectAsync(ev, CancellationToken.None);

        using var ctx = new AgentSmithDbContext(Options());
        ctx.RunEvents.Count(e => e.RunId == "run-1")
            .Should().Be(stream.Count, "every event lands in the trail, flushed on RunFinished");
        ctx.RunEvents.Should().OnlyContain(e => e.PayloadJson != null && e.Seq >= 0);
    }

    [Fact]
    public async Task Projector_ActiveRun_AppearsInActiveList_ThenMovesToRecent()
    {
        var projector = NewProjector();
        var t = _clock.Now;
        await projector.ProjectAsync(
            new RunStartedEvent("run-1", "ticket", "fix-bug", new[] { "primary" }, t, "claude", "42"),
            CancellationToken.None);

        (await NewStore().GetActiveRunsAsync(CancellationToken.None))
            .Should().ContainSingle(r => r.Id == "run-1");

        await projector.ProjectAsync(
            new RunFinishedEvent("run-1", "success", null, "done", t, 0m), CancellationToken.None);

        (await NewStore().GetActiveRunsAsync(CancellationToken.None)).Should().BeEmpty();
        (await NewStore().GetRecentRunsAsync(10, CancellationToken.None))
            .Should().ContainSingle(r => r.Id == "run-1" && r.Status == "success");
    }

    [Fact]
    public async Task Retention_PrunesOldTrailEvents_KeepsRunSummary()
    {
        var old = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
        var projector = NewProjector();
        foreach (var ev in SampleStream("run-1", old))
            await projector.ProjectAsync(ev, CancellationToken.None);

        var retention = new RunRetentionService(NewFactory(), _clock);
        var pruned = await retention.PruneAsync(TimeSpan.FromDays(30), CancellationToken.None);

        pruned.Should().BeGreaterThan(0, "the aged trail events are pruned");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.RunEvents.Should().BeEmpty("the aged raw trail is gone");
        var run = ctx.Runs.Single(r => r.Id == "run-1");
        run.Summary.Should().Be("Fixed the bug", "the run summary is KEPT — history survives retention");
    }

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<AgentSmithDbContext>
    {
        public AgentSmithDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public void Dispose() => _connection.Dispose();
}
