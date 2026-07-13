using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0327: the applier's checkpoint projection, split out of
/// <see cref="RunEventApplier"/> like <see cref="QueuedRunProjection"/>. A
/// RunCheckpointedEvent upserts the run's single checkpoint row (unique RunId);
/// a replayed event converges on the same row. A fresh checkpoint resets
/// ResumedAt — the run parked again on a later question.
/// </summary>
internal static class RunCheckpointProjection
{
    public static async Task UpsertAsync(
        IUnitOfWork uow, RunCheckpointedEvent e, CancellationToken ct)
    {
        var row = await uow.Set<RunCheckpoint>()
            .FirstOrDefaultAsync(c => c.RunId == e.RunId, ct);
        if (row is null)
        {
            row = new RunCheckpoint { RunId = e.RunId };
            uow.Add(row);
        }
        row.Project = e.Project;
        row.TicketId = e.TicketId;
        row.Platform = e.Platform;
        row.Pipeline = e.Pipeline;
        row.DialogueJobId = e.DialogueJobId;
        row.QuestionId = e.QuestionId;
        row.QuestionJson = e.QuestionJson;
        row.RemainingCommandsJson = e.RemainingCommandsJson;
        row.ContextJson = e.ContextJson;
        row.ExecutionCount = e.ExecutionCount;
        row.AskedAt = e.AskedAt;
        row.AnswerDeadlineAt = e.AnswerDeadlineAt;
        row.ResumedAt = null;
        await uow.SaveChangesAsync(ct);
    }
}
