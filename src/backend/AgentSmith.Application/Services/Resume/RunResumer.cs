using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0327: rehydration entry point. Packages a pending checkpoint + its answer
/// as a ResumePayload and enqueues it through the EXISTING capacity queue
/// (IsResume entry, reserved run id = the checkpointed run's own id) — resumed
/// runs compete for capacity like new ones, no priority lane, and a
/// capacity-denied resume simply waits in line on its existing run row. The
/// queue upsert is idempotent per (project, ticket), so a racing duplicate
/// collapses; the checkpoint is marked consumed after the enqueue landed.
/// </summary>
public sealed class RunResumer(
    ICapacityQueue capacityQueue,
    IRunCheckpointStore checkpointStore,
    ILogger<RunResumer> logger) : IRunResumer
{
    public async Task<bool> EnqueueResumeAsync(
        RunCheckpointRecord checkpoint, DialogAnswer answer, CancellationToken cancellationToken)
    {
        if (checkpoint.ResumedAt is not null) return false;
        if (string.IsNullOrEmpty(checkpoint.Project) || string.IsNullOrEmpty(checkpoint.TicketId))
        {
            logger.LogWarning(
                "Checkpoint for run {RunId} has no project/ticket — cannot ride the capacity queue; abandoning",
                checkpoint.RunId);
            await checkpointStore.TryMarkResumedAsync(checkpoint.RunId, DateTimeOffset.UtcNow, cancellationToken);
            return false;
        }

        await capacityQueue.EnqueueAsync(BuildCandidate(checkpoint, answer), cancellationToken);
        var marked = await checkpointStore.TryMarkResumedAsync(
            checkpoint.RunId, DateTimeOffset.UtcNow, cancellationToken);
        logger.LogInformation(
            "Run {RunId} resume enqueued (question '{QuestionId}' answered by {By})",
            checkpoint.RunId, answer.QuestionId, answer.AnsweredBy);
        return marked;
    }

    private static CapacityQueueCandidate BuildCandidate(
        RunCheckpointRecord checkpoint, DialogAnswer answer)
    {
        var payload = new ResumePayload(
            Commands: JsonSerializer.Deserialize<List<CheckpointCommand>>(checkpoint.RemainingCommandsJson)
                ?? throw new InvalidOperationException(
                    $"Checkpoint for run {checkpoint.RunId} has no remaining commands."),
            ContextJson: checkpoint.ContextJson,
            ExecutionCount: checkpoint.ExecutionCount,
            QuestionJson: checkpoint.QuestionJson,
            AnswerJson: JsonSerializer.Serialize(answer));
        var initialContext = new Dictionary<string, object>
        {
            [ContextKeys.ResumeCheckpoint] = JsonSerializer.Serialize(payload),
        };
        return new CapacityQueueCandidate(
            Project: checkpoint.Project,
            TicketId: checkpoint.TicketId,
            Pipeline: checkpoint.Pipeline,
            Platform: checkpoint.Platform ?? string.Empty,
            CandidateRunId: checkpoint.RunId,
            Reason: $"resuming — answer received from {answer.AnsweredBy}",
            Repos: [],
            InitialContextJson: JsonSerializer.Serialize(initialContext),
            PlanAnswersJson: null,
            IsResume: true);
    }
}
