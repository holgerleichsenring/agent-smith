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
/// </summary>
public sealed class MasterOpenQuestionsHandler(
    IPlanOpenQuestionsPoster poster,
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

        context.Pipeline.TryGet<string>(ContextKeys.NeedsClarificationStatus, out var parkStatus);
        await poster.PostAsync(
            context.TrackerConnection, context.Ticket.Id, questions, parkStatus, cancellationToken);

        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        var parked = string.IsNullOrWhiteSpace(parkStatus)
            ? "(not parked — needs_clarification_status unset)"
            : $"(parked -> {parkStatus})";
        logger.LogInformation(
            "Master mid-run question posted to ticket {Ticket} {Parked}",
            context.Ticket.Id.Value, parked);
        return CommandResult.Ok(
            $"awaiting_user_input: {questions.Count} master question(s) posted {parked}");
    }
}
