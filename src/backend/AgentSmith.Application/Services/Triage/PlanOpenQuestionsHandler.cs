using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Runs after the Plan-skill / GeneratePlan step. When the Plan declares
/// status=NeedsUserInput, posts a structured open-questions comment via
/// IPlanOpenQuestionsPoster and sets ContextKeys.OpenQuestionsAwaitingAnswer
/// so the executor halts cleanly. Status=Complete is a no-op.
/// </summary>
public sealed class PlanOpenQuestionsHandler(
    IPlanOpenQuestionsPoster poster,
    ILogger<PlanOpenQuestionsHandler> logger)
    : ICommandHandler<PlanOpenQuestionsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        PlanOpenQuestionsContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<Plan>(ContextKeys.Plan, out var plan) || plan is null)
        {
            logger.LogDebug("No Plan in context; skipping open-questions check");
            return CommandResult.Ok("No Plan in context");
        }

        if (plan.Status != PlanStatus.NeedsUserInput)
            return CommandResult.Ok("Plan status=Complete; no open questions");

        if (plan.OpenQuestions.Count == 0)
        {
            logger.LogWarning(
                "Plan declared NeedsUserInput but emitted no open_questions; nothing to post");
            return CommandResult.Ok("Plan needs input but produced no questions");
        }

        await poster.PostAsync(
            context.TrackerConnection, context.Ticket.Id, plan.OpenQuestions, cancellationToken);

        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        return CommandResult.Ok(
            $"awaiting_user_input: {plan.OpenQuestions.Count} question(s) posted");
    }
}
