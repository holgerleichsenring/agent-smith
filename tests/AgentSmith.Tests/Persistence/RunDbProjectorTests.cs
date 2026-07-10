using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    // p0246g: the projector is a facade over a scoped unit of work. Build a tiny
    // provider whose scoped IUnitOfWork is a fresh context over the SAME in-memory
    // connection — each scope sees the same database.
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunDbProjector>();
        return services.BuildServiceProvider();
    }

    private Factory NewFactory() => new(_connection);
    private RunDbProjector NewProjector() => BuildProvider().GetRequiredService<RunDbProjector>();
    private RunRepository NewStore() => new(new AgentSmithDbContext(Options()));

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
    public async Task LlmCallFinished_CarriesCachedTokens_PersistedOnRunLlmCall()
    {
        // p0323: the cached share is part of the durable per-call record — an
        // always-0 column is the alarm that the caching strategy died again.
        var projector = NewProjector();
        var t = _clock.Now;
        await projector.ProjectAsync(
            new RunStartedEvent("run-cache", "ticket", "fix-bug", new[] { "primary" }, t, "claude", "42"),
            CancellationToken.None);
        await projector.ProjectAsync(
            new LlmCallFinishedEvent(
                "run-cache", "claude-sonnet-4-6", "coding-agent",
                TokensIn: 2_000, TokensOut: 300, CostUsd: 0.02m, DurationMs: 900, Timestamp: t,
                Phase: "implementation", RepoName: "primary",
                CachedTokensIn: 18_000, CacheCreationTokensIn: 2_500),
            CancellationToken.None);

        var run = await NewStore().GetRunDetailAsync("run-cache", CancellationToken.None);

        var call = run!.LlmCalls.Should().ContainSingle().Subject;
        call.CachedTokensIn.Should().Be(18_000);
        call.CacheCreationTokensIn.Should().Be(2_500);
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
    public async Task Applier_StepStarted_GrowsTotalSteps_NeverShrinks()
    {
        // p0322a: the producer recomputes TotalSteps from the LIVE command list on
        // every step — it grows when BootstrapDispatch splices rounds mid-run. The
        // projection keeps the max so out-of-order/duplicate replays never shrink it.
        var t = _clock.Now;
        var projector = NewProjector();
        await projector.ProjectAsync(
            new RunStartedEvent("run-1", "ticket", "init-project", new[] { "primary" }, t, "claude", "42"),
            CancellationToken.None);

        await projector.ProjectAsync(new StepStartedEvent("run-1", 0, "LoadCatalog", 11, t), CancellationToken.None);
        (await NewStore().GetRunDetailAsync("run-1", CancellationToken.None))!
            .TotalSteps.Should().Be(11);

        await projector.ProjectAsync(new StepStartedEvent("run-1", 7, "BootstrapRound", 16, t), CancellationToken.None);
        await projector.ProjectAsync(new StepStartedEvent("run-1", 8, "WriteRunResult", 13, t), CancellationToken.None);

        (await NewStore().GetRunDetailAsync("run-1", CancellationToken.None))!
            .TotalSteps.Should().Be(16, "the max total seen wins — a lower late value never shrinks it");
    }

    [Fact]
    public async Task Projector_CancelRequested_PersistsFlagAndReason_SurvivesNavigation()
    {
        // p0259: RunCancelRequestedEvent was trail-only, so a navigated/reloaded
        // detail view (read from this DB projection) saw CancelRequested=false and
        // showed "cancel" instead of "cancelling…". Persisting it is the fix.
        var t = _clock.Now;
        var projector = NewProjector();
        await projector.ProjectAsync(
            new RunStartedEvent("run-1", "ticket", "fix-bug", new[] { "primary" }, t, "claude", "42"),
            CancellationToken.None);
        await projector.ProjectAsync(
            new RunCancelRequestedEvent("run-1", "operator", t), CancellationToken.None);

        // A fresh store = a navigation/reload that reads from the system-of-record.
        var run = await NewStore().GetRunDetailAsync("run-1", CancellationToken.None);

        run.Should().NotBeNull();
        run!.CancelRequested.Should().BeTrue("the cancelling state must survive navigation");
        run.CancelReason.Should().Be("operator");
    }

    [Fact]
    public async Task Projector_OperatorCancel_TerminatesAsCancelled_NotFailed()
    {
        // p0259: an operator/watchdog cancel is its own terminal status — the
        // dashboard must read intent, not a crash.
        var t = _clock.Now;
        var projector = NewProjector();
        await projector.ProjectAsync(
            new RunStartedEvent("run-1", "ticket", "fix-bug", new[] { "primary" }, t, "claude", "42"),
            CancellationToken.None);
        await projector.ProjectAsync(
            new RunCancelRequestedEvent("run-1", "operator", t), CancellationToken.None);
        await projector.ProjectAsync(
            new RunFinishedEvent("run-1", "cancelled", null, "Cancelled by operator.", t, 0m),
            CancellationToken.None);

        var run = await NewStore().GetRunDetailAsync("run-1", CancellationToken.None);

        run!.Status.Should().Be("cancelled");
        run.CancelRequested.Should().BeTrue("the cancel flag persists across the terminal event");
    }

    [Fact]
    public async Task Retention_PrunesOldTrailEvents_KeepsRunSummary()
    {
        var old = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
        var projector = NewProjector();
        foreach (var ev in SampleStream("run-1", old))
            await projector.ProjectAsync(ev, CancellationToken.None);

        var retention = new RunRetentionService(new AgentSmithDbContext(Options()), _clock);
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
