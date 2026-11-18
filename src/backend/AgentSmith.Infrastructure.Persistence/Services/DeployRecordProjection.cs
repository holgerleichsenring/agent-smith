using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-18-3863f: a deploy's delivery becomes its record — inserted once; a replayed event finds the row and leaves
/// it alone, so what the follow has read since is never reset. A pipeline run the host did not name is inserted
/// settled: nothing can read it back.
/// </summary>
public sealed class DeployRecordProjection
{
    public async Task ApplyAsync(IUnitOfWork uow, DeployDeliveredEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (await uow.Set<DeployRecord>().AnyAsync(r => r.RunId == e.RunId, ct)) return;
        var unnamed = e.PullRequestUrl is null && e.PipelineRunId is null;
        uow.Add(new DeployRecord
        {
            RunId = e.RunId, Project = e.Project, Context = e.Context, ContextRepo = e.ContextRepo, Environment = e.Environment,
            Version = e.Version, Delivery = e.Delivery, Repo = e.Repo, Url = e.Url, PullRequestUrl = e.PullRequestUrl,
            PipelineRunId = e.PipelineRunId, CiState = CiStates.Pending,
            PromotesTo = e.PromotesTo is { Count: > 0 } to ? string.Join(',', to) : null, // 2026-11-18-3863h DeliveredAtTicks = e.Timestamp.UtcTicks,
            SettledAtTicks = unnamed ? e.Timestamp.UtcTicks : null,
            Note = unnamed ? "the host started the pipeline without naming the run, so it is not followed" : null,
        });
        await uow.SaveChangesAsync(ct);
    }
}
