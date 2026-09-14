using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// p0315d: the mid-run sibling of the p0318 clarification gate. When the
/// executing master asked a question via ask_human (captured by
/// TicketClarificationToolHost, published as ContextKeys.MasterOpenQuestions),
/// this step posts it as the SAME anchored open-questions ticket comment,
/// parks the ticket in needs_clarification_status and sets the
/// awaiting-answer flag — so the executor short-circuits the rest of the run
/// (no record, no PR) and the answer + status move re-trigger a fresh run.
/// No question captured → clean no-op.
/// <para>
/// p0453: it also checkpoints, so the SAME run can be resumed from the dashboard. The
/// ticket comment remains how a human who is not watching the dashboard learns of the
/// question; the checkpoint is what makes answering it not require a status move.
/// </para>
/// <para>
/// 2026-09-03-3c07: the resumed run re-enters HERE (the cursor starts with the asking
/// step). An answer the resume delivered for this ask is handed to the master by
/// splicing the master and this step back in front of the remaining block — the master
/// continues from the answer instead of the run re-asking, or parking, on it.
/// </para>
/// </summary>
public sealed class MasterOpenQuestionsHandler(
    IPlanOpenQuestionsPoster poster,
    IClarificationParkStatusResolver parkStatus,
    MasterQuestionCheckpoint checkpoint,
    IMasterAnswerIntake answerIntake,
    ILogger<MasterOpenQuestionsHandler> logger)
    : ICommandHandler<MasterOpenQuestionsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        MasterOpenQuestionsContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<PlanOpenQuestion>>(
                ContextKeys.MasterOpenQuestions, out var questions)
            || questions is not { Count: > 0 })
            return CommandResult.Ok("Master asked no mid-run question");

        if (await answerIntake.TryDeliverAsync(context.Pipeline, questions) is { } answer)
            return Reengage(context.Step, answer);

        var status = parkStatus.TryResolve(context.Pipeline, context.TrackerConnection);
        if (status is null)
        {
            logger.LogError("Master question cannot park: {Reason}", parkStatus.UnresolvedReason);
            return CommandResult.Fail(parkStatus.UnresolvedReason);
        }

        await poster.PostAsync(
            context.Pipeline, context.TrackerConnection, context.Ticket, questions, status,
            cancellationToken);

        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        // p0453: and make it answerable where it is SHOWN. Without a checkpoint the
        // dashboard has no question to render and nowhere to send a reply, so the only way
        // back into the run is a manual status move on the board.
        await checkpoint.WriteAsync(context.Pipeline, questions, cancellationToken);
        logger.LogInformation(
            "Master mid-run question posted to ticket {Ticket} (parked -> {Status})",
            context.Ticket.Id.Value, status);
        return CommandResult.Ok(
            $"awaiting_user_input: {questions.Count} master question(s) posted (parked -> {status})");
    }

    // The master, then this step again so a second question parks the same way — the rest
    // of the block (commit, verify, record) is already ahead of the cursor.
    private static CommandResult Reengage(PipelineCommand step, DialogAnswer answer) =>
        CommandResult.OkAndContinueWith(
            $"operator answered: {answer.Answer} — re-engaging the master",
            new PipelineCommand(CommandNames.AgenticMaster) { PhaseId = step.PhaseId },
            new PipelineCommand(CommandNames.MasterOpenQuestions) { PhaseId = step.PhaseId });
}
