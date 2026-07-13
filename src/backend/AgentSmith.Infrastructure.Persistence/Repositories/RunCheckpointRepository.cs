using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0327: data access for run checkpoints over a scoped unit of work. One row
/// per run (unique RunId — a re-checkpoint of the same run upserts in place);
/// TryMarkResumed is a guarded ExecuteUpdate so exactly one writer consumes a
/// pending checkpoint.
/// </summary>
public sealed class RunCheckpointRepository(IUnitOfWork unitOfWork)
{
    public async Task SaveAsync(RunCheckpointRecord checkpoint, CancellationToken ct)
    {
        var existing = await unitOfWork.Set<RunCheckpoint>()
            .FirstOrDefaultAsync(c => c.RunId == checkpoint.RunId, ct);
        if (existing is null)
        {
            existing = new RunCheckpoint { RunId = checkpoint.RunId };
            unitOfWork.Add(existing);
        }
        Apply(checkpoint, existing);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<RunCheckpointRecord?> GetByRunIdAsync(string runId, CancellationToken ct)
    {
        var row = await unitOfWork.Set<RunCheckpoint>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.RunId == runId, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<IReadOnlyList<RunCheckpointRecord>> ListPendingAsync(CancellationToken ct)
    {
        var rows = await unitOfWork.Set<RunCheckpoint>().AsNoTracking()
            .Where(c => c.ResumedAt == null)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<bool> TryMarkResumedAsync(string runId, DateTimeOffset resumedAt, CancellationToken ct)
    {
        var updated = await unitOfWork.Set<RunCheckpoint>()
            .Where(c => c.RunId == runId && c.ResumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ResumedAt, resumedAt), ct);
        return updated > 0;
    }

    private static void Apply(RunCheckpointRecord source, RunCheckpoint target)
    {
        target.Project = source.Project;
        target.TicketId = source.TicketId;
        target.Platform = source.Platform;
        target.Pipeline = source.Pipeline;
        target.DialogueJobId = source.DialogueJobId;
        target.QuestionId = source.QuestionId;
        target.QuestionJson = source.QuestionJson;
        target.RemainingCommandsJson = source.RemainingCommandsJson;
        target.ContextJson = source.ContextJson;
        target.ExecutionCount = source.ExecutionCount;
        target.AskedAt = source.AskedAt;
        target.AnswerDeadlineAt = source.AnswerDeadlineAt;
        target.ResumedAt = source.ResumedAt;
    }

    private static RunCheckpointRecord ToRecord(RunCheckpoint c) => new(
        c.RunId, c.Project, c.TicketId, c.Platform, c.Pipeline,
        c.DialogueJobId, c.QuestionId, c.QuestionJson,
        c.RemainingCommandsJson, c.ContextJson, c.ExecutionCount,
        c.AskedAt, c.AnswerDeadlineAt, c.ResumedAt);
}
