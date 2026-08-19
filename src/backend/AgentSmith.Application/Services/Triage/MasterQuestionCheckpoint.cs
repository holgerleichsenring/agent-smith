using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// p0453: the mid-run question becomes answerable where it is shown.
/// <para>
/// Two ask paths existed and only one was answerable. p0327's gate writes a checkpoint, so
/// the dashboard renders the question and the operator answers it in place and the SAME run
/// resumes. p0315d's mid-run master question posted a ticket comment, parked the ticket and
/// ended the run — no checkpoint, so `pendingQuestion` was null and the card read "Question
/// unavailable — open the run to answer", where there was equally nothing to answer.
/// </para>
/// <para>
/// The operator's way out was a manual status move on the board. Live run 216b sat on
/// exactly that: the answer was on the ticket at 10:55 and nothing happened, because a
/// parked ticket is deliberately outside the trigger statuses.
/// </para>
/// <para>
/// This does not replace the ticket comment. The comment is how a human who is not looking
/// at the dashboard learns of the question, and the ticket stays the record; the checkpoint
/// is what makes the run resumable without one.
/// </para>
/// </summary>
public sealed class MasterQuestionCheckpoint(
    IDialogueCheckpointWriter writer,
    ILogger<MasterQuestionCheckpoint> logger)
{
    /// <summary>
    /// The question waits as long as the operator needs. A mid-run ask has no deadline of
    /// its own — the run is parked and holds nothing but its branch.
    /// </summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromDays(14);

    public async Task<bool> WriteAsync(
        PipelineContext pipeline, IReadOnlyList<PlanOpenQuestion> questions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count == 0) return false;
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
        {
            logger.LogWarning("Mid-run question cannot be checkpointed — the run has no id");
            return false;
        }

        var written = await writer.TryCheckpointAsync(pipeline, Compose(questions), runId!, ct);
        if (!written)
            logger.LogWarning(
                "Mid-run question was posted to the ticket but not checkpointed — it can only "
                + "be answered there, not in the dashboard");
        return written;
    }

    /// <summary>
    /// One question carries the master's ask. Several are rendered as one text because the
    /// operator answers them together, in one reply, exactly as the ticket comment asks.
    /// </summary>
    internal static DialogQuestion Compose(IReadOnlyList<PlanOpenQuestion> questions)
    {
        var first = questions[0];
        var text = questions.Count == 1
            ? first.Question
            : string.Join("\n", questions.Select((q, i) => $"Q{i + 1}: {q.Question}"));
        var choices = questions.Count == 1 && first.Options.Count > 0
            ? questions[0].Options.Select(o => new DialogChoice(o, o)).ToList()
            : null;
        return new DialogQuestion(
            first.Id, choices is null ? QuestionType.FreeText : QuestionType.Choice,
            text, Context: null, choices, DefaultAnswer: null, Patience);
    }
}
