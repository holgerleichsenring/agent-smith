using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-10-02-5ab2b: the server composition over a shared SQLite file with a queue of the
/// test's choosing — the durable spine of <see cref="DurableDialogueHarness"/>, the launch an
/// init makes (its pre-start row, then the production dispatch), and the minute that makes a
/// stored request count as lost.
/// </summary>
public static class QueuedRunRecoveryHarness
{
    public const string Fixture = "agentsmith-dialogue.yml";
    public const string Project = "fixture-durable-dialogue";

    public static RealCompositionHarness Build(string dbPath, IRedisJobQueue queue) =>
        RealCompositionHarness.Build(
            FixturePaths.For(Fixture), SandboxBackend.Stub, session: null, SkillsBackend.Stub,
            services =>
            {
                DurableDialogueHarness.RegisterDurableSpine(services, dbPath, new RecordingJobQueue());
                services.RemoveAll<IRedisJobQueue>();
                services.AddSingleton(queue);
            });

    /// <summary>What InitRunLauncher does once admitted: the queued row, then the dispatch —
    /// with the same two launch flags in the request's context.</summary>
    public static async Task LaunchInitAsync(RealCompositionHarness harness, string runId, bool autoComplete)
    {
        await using (var scope = harness.Services.CreateAsyncScope())
            await new InitRunRepository(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), TimeProvider.System)
                .CreateQueuedRunAsync(runId, Project, "init-project", ["repo"], "starting", CancellationToken.None);
        await harness.Services.GetRequiredService<QueuedRunDispatch>().DispatchAsync(
            new PipelineRequest(Project, "init-project", IsInit: true, Headless: true, RunId: runId,
                Context: new Dictionary<string, object>
                {
                    [ContextKeys.AutoCompletePullRequests] = autoComplete,
                    [ContextKeys.RefreshPrinciples] = false,
                }),
            CancellationToken.None);
    }

    /// <summary>Moves the stored request's push time past the sweeper's 60-second bound.</summary>
    public static async Task AgeRequestAsync(string dbPath, string runId)
    {
        DateTimeOffset? aged = DateTimeOffset.UtcNow - QueuedRunSweeper.LostAfter * 2;
        await using var db = Db(dbPath);
        await db.Runs.Where(r => r.Id == runId).ExecuteUpdateAsync(s => s.SetProperty(r => r.RequestEnqueuedAt, aged));
    }

    public static AgentSmithDbContext Db(string dbPath) => new(
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={dbPath}").Options);

    public static void DeleteStore(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
            if (File.Exists(file)) try { File.Delete(file); } catch (IOException) { /* best-effort */ }
    }
}
