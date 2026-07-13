using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0327: builds and publishes the RunCheckpointedEvent when a dialogue wait
/// crosses the hot threshold. The remaining-work list = the deterministic
/// re-provisioning steps that already ran (re-checkout into fresh sandboxes —
/// sandboxes are cattle) + the executor's live cursor, which starts with the
/// asking step itself so the resumed run re-enters it and consumes the answer.
/// Publishing an EVENT (not a DB write) keeps this working from a spawned
/// orchestrator, whose only DB channel is the event stream (p0330 lesson).
/// </summary>
public sealed class DialogueCheckpointWriter(
    IEventPublisher eventPublisher,
    IPipelineContextSerializer contextSerializer,
    ILogger<DialogueCheckpointWriter> logger) : IDialogueCheckpointWriter
{
    // Steps that must re-run before the cursor: they materialize the sandbox
    // working tree (checkout) and its credentials, which the checkpoint
    // deliberately does not preserve.
    private static readonly string[] ReprovisionOrder =
        [CommandNames.CheckoutSource, CommandNames.SetupRegistryAuth];

    public async Task<bool> TryCheckpointAsync(
        PipelineContext pipeline, DialogQuestion question, string dialogueJobId,
        CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)
            || !pipeline.TryGet<IReadOnlyList<PipelineCommand>>(ContextKeys.RemainingCommands, out var remaining)
            || remaining is null
            || !pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
        {
            logger.LogWarning(
                "Checkpoint for question '{QuestionId}' skipped — run id, step cursor or ticket missing",
                question.QuestionId);
            return false;
        }

        var commands = BuildResumeCommands(pipeline, remaining);
        pipeline.TryGet<int>(ContextKeys.PipelineExecutionCount, out var executionCount);
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var project);
        pipeline.TryGet<string>(ContextKeys.TrackerPlatform, out var platform);
        pipeline.TryGet<string>(ContextKeys.PipelineName, out var pipelineName);

        var now = DateTimeOffset.UtcNow;
        await eventPublisher.PublishAsync(new RunCheckpointedEvent(
            runId, project ?? string.Empty, ticketId.Value, platform, pipelineName ?? string.Empty,
            dialogueJobId, question.QuestionId,
            QuestionJson: JsonSerializer.Serialize(question),
            RemainingCommandsJson: JsonSerializer.Serialize(commands),
            ContextJson: contextSerializer.Serialize(pipeline),
            ExecutionCount: executionCount,
            AskedAt: now, AnswerDeadlineAt: now + RemainingTimeout(question),
            Timestamp: now), cancellationToken);

        logger.LogInformation(
            "Run {RunId} checkpointed on question '{QuestionId}' — {Count} commands remain, answer deadline {Deadline}",
            runId, question.QuestionId, commands.Count, now + RemainingTimeout(question));
        return true;
    }

    private static List<CheckpointCommand> BuildResumeCommands(
        PipelineContext pipeline, IReadOnlyList<PipelineCommand> remaining)
    {
        var executed = pipeline.TryGet<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail, out var trail)
            ? trail!.Select(t => t.CommandName).ToHashSet(StringComparer.Ordinal)
            : [];
        var reprovision = ReprovisionOrder
            .Where(executed.Contains)
            .Select(name => new CheckpointCommand(name));
        return reprovision.Concat(remaining.Select(CheckpointCommand.From)).ToList();
    }

    // The deadline is anchored at the ASK, and the hot window already elapsed —
    // but Timeout is measured from the ask, so the full span applies from here
    // minus what was already waited. The hot window is small against a days-scale
    // timeout; anchoring at checkpoint time keeps the math simple and generous.
    private static TimeSpan RemainingTimeout(DialogQuestion question) => question.Timeout;
}
