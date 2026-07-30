using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// The clarification gate (p0318). Runs after GeneratePlan, before Approval/AgenticMaster.
/// Halts the run when the agent cannot sensibly proceed:
///   (1) the planner returned status=NeedsUserInput (the semantic judgment — handles a
///       ticket that HAS a body but is still unworkable), or
///   (2) the effective ticket body is empty (the deterministic pre-guard — a title-only
///       ticket where nothing reached the planner; the incident that motivated p0318).
/// On halt it posts the open questions (or a synthesized clarification ask) and parks the
/// ticket in the configured needs_clarification_status so discovery does not re-claim it — the
/// human moving it back to a work status is the re-trigger. Status=Complete with a non-empty
/// body is a no-op. p0391: an unset park status is a configuration error raised by the
/// resolver, never a silent "(not parked)" degrade.
/// </summary>
public sealed class PlanOpenQuestionsHandler(
    IPlanOpenQuestionsPoster poster,
    IClarificationParkStatusResolver parkStatus,
    ILogger<PlanOpenQuestionsHandler> logger)
    : ICommandHandler<PlanOpenQuestionsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        PlanOpenQuestionsContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.TryGet<Plan>(ContextKeys.Plan, out var plan);

        var needsInput = plan?.Status == PlanStatus.NeedsUserInput;
        var emptyBody = string.IsNullOrWhiteSpace(context.Ticket.Description);
        if (!needsInput && !emptyBody)
            return CommandResult.Ok("Plan complete and ticket has a body; no clarification needed");

        // Prefer the planner's own questions; fall back to a synthesized ask (empty body,
        // or NeedsUserInput with no captured questions) so the run never halts silently.
        var questions = plan is { OpenQuestions.Count: > 0 }
            ? plan.OpenQuestions
            : [SyntheticQuestion(emptyBody)];

        var status = parkStatus.Resolve(context.Pipeline, context.TrackerConnection);
        await poster.PostAsync(
            context.TrackerConnection, context.Ticket.Id, questions, status, cancellationToken);

        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        logger.LogInformation(
            "Clarification gate posted {Count} question(s) on ticket {Ticket} (parked -> {Status})",
            questions.Count, context.Ticket.Id.Value, status);
        return CommandResult.Ok(
            $"awaiting_user_input: {questions.Count} question(s) posted (parked -> {status})");
    }

    private static PlanOpenQuestion SyntheticQuestion(bool emptyBody) => new(
        "clarify",
        emptyBody
            ? "This ticket has no description or reproduction steps, so there is nothing to "
              + "implement. Please describe the expected behaviour and how to reproduce the "
              + "issue, then move the ticket back to a work status."
            : "The plan could not be completed without more information. Please add the missing "
              + "detail to the ticket, then move it back to a work status.",
        []);
}
