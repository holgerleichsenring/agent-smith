using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-23-8df6a: the rows the Deployments page reads — a code run with its pull-request record, and a deploy
/// record — written into a migrated store.</summary>
internal sealed class DeploymentsPageSeed(SqliteConnection connection)
{
    public static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A run of <paramref name="project"/> and its pull request in <paramref name="state"/> since <paramref name="at"/>;
    /// <paramref name="withRun"/> false leaves the run out, as a deleted one is.</summary>
    public async Task MergedAsync(string project, string runId, DateTimeOffset at, string state = PrReviewStates.Merged,
        string? mergeSha = "m1", bool withRun = true)
    {
        await using var db = MigratedStoreTemplate.Context(connection);
        if (withRun)
            db.Runs.Add(new Run { Id = runId, Project = project, Pipeline = "code", Status = "success", TicketTitle = $"Title {runId}" });
        var url = $"https://github.com/o/{project.TrimStart('@')}/pull/{runId}";
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "api", Number = runId, Project = project, TicketId = $"T-{runId}", RunId = runId,
            State = state, StateAtTicks = at.UtcTicks, CheckedAtTicks = at.UtcTicks, TargetBranch = "main", MergeSha = mergeSha,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeployedAsync(string project, string runId, DateTimeOffset at)
    {
        await using var db = MigratedStoreTemplate.Context(connection);
        db.Add(new DeployRecord
        {
            RunId = runId, Project = project, Context = "api", Environment = "test", Version = "v1", Delivery = "pipeline_trigger",
            Repo = "app", Url = "https://ci.example/runs/1", CiState = CiStates.Green, DeliveredAtTicks = at.UtcTicks,
        });
        await db.SaveChangesAsync();
    }
}
