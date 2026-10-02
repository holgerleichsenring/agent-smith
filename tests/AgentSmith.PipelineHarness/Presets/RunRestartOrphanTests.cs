using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-02-5f89e: the operator's finding, through the REAL composition. An init run is
/// running when its server restarts; the new process starts with an empty cancellation
/// registry, the run holds no lease, and nothing renews its row. The second "process" —
/// a second harness over the same SQLite file — ends it on its own: the liveness reaper
/// flags it interrupted, the cancel enforcer finishes it cancelled. No operator.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class RunRestartOrphanTests
{
    private const string Fixture = "agentsmith-dialogue.yml";
    private const string Project = "fixture-durable-dialogue";
    private const string RunId = "2026-10-02T09-43-00-5f89";

    [Fact]
    public async Task Restart_InitRunNoProcessDrives_EndsCancelledReasonInterrupted()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");
        var jobQueue = new RecordingJobQueue();
        try
        {
            await using (var first = Build(dbPath, jobQueue))
            {
                await DurableDialogueHarness.MigrateAsync(first);
                await StartInitRunAsync(first, startedAt: DateTimeOffset.UtcNow.AddMinutes(-10));
            }

            await using var second = Build(dbPath, jobQueue);
            var flagged = await second.Services.GetRequiredService<RunLivenessReaper>()
                .RunOnceAsync(CancellationToken.None);
            var enforced = await second.Services.GetRequiredService<CancelEnforcer>()
                .RunOnceAsync(CancellationToken.None);

            flagged.Should().Be(1);
            enforced.Should().Be(1);
            using var ctx = Db(dbPath);
            var run = ctx.Runs.Single(r => r.Id == RunId);
            run.Status.Should().Be("cancelled");
            run.FinishedAt.Should().NotBeNull();
            run.CancelReason.Should().Be("interrupted");
            run.Summary.Should().Be(CancelReasonNarrator.Summary("interrupted"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                if (File.Exists(f)) try { File.Delete(f); } catch (IOException) { /* best-effort */ }
        }
    }

    private static RealCompositionHarness Build(string dbPath, RecordingJobQueue jobQueue) =>
        RealCompositionHarness.Build(
            FixturePaths.For(Fixture), SandboxBackend.Stub, session: null, SkillsBackend.Stub,
            services => DurableDialogueHarness.RegisterDurableSpine(services, dbPath, jobQueue));

    // The rows a manual init leaves when its run has started: the launcher's pre-start row,
    // promoted to running by RunStarted — and then the process that drove it is gone.
    private static async Task StartInitRunAsync(RealCompositionHarness harness, DateTimeOffset startedAt)
    {
        await using (var scope = harness.Services.CreateAsyncScope())
            await new InitRunRepository(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), TimeProvider.System)
                .CreateQueuedRunAsync(RunId, Project, "init-project", ["repo"], "starting", CancellationToken.None);
        await harness.Services.GetRequiredService<IEventPublisher>().PublishAsync(
            new RunStartedEvent(RunId, "manual", "init-project", ["repo"], startedAt, "claude", null,
                Project: Project, Platform: null),
            CancellationToken.None);
    }

    private static AgentSmithDbContext Db(string dbPath) => new(
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={dbPath}").Options);
}
