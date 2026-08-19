using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
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
/// </summary>
public sealed class MasterOpenQuestionsHandler(
    IPlanOpenQuestionsPoster poster,
    IClarificationParkStatusResolver parkStatus,
    MasterQuestionCheckpoint checkpoint,
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

        var status = parkStatus.TryResolve(context.Pipeline, context.TrackerConnection);
        if (status is null)
        {
            logger.LogError("Master question cannot park: {Reason}", parkStatus.UnresolvedReason);
            return CommandResult.Fail(parkStatus.UnresolvedReason);
        }

        await poster.PostAsync(
            context.TrackerConnection, context.Ticket, questions, status, cancellationToken);

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
}
