using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: the phase-execution entry gate. Extracts the fenced yaml spec out
/// of the phase ticket (inverse of the p0315c renderer, AzDO HTML variant
/// included), publishes the validated <see cref="PhaseDraft"/> as
/// <see cref="ContextKeys.PhaseSpec"/> and its steps as the approved
/// <see cref="ContextKeys.Plan"/> the master executes. A phase-labelled
/// ticket without a schema-valid spec fails the run loudly HERE — before any
/// master tokens are spent on a broken artifact.
/// </summary>
public sealed class PhaseSpecGateHandler(
    IPhaseSpecFromTicket specFromTicket,
    PhaseSpecPlanFactory planFactory,
    ILogger<PhaseSpecGateHandler> logger)
    : ICommandHandler<PhaseSpecGateContext>
{
    public Task<CommandResult> ExecuteAsync(
        PhaseSpecGateContext context, CancellationToken cancellationToken)
    {
        var extraction = specFromTicket.Extract(context.Ticket.Description);
        if (extraction is PhaseSpecInvalid invalid)
        {
            logger.LogWarning(
                "Phase ticket {Ticket} carries no executable spec: {Error}",
                context.Ticket.Id.Value, invalid.Error);
            return Task.FromResult(CommandResult.Fail(
                $"Phase ticket {context.Ticket.Id.Value} carries no executable spec: {invalid.Error}"));
        }

        var draft = ((PhaseSpecExtracted)extraction).Draft;
        context.Pipeline.Set(ContextKeys.PhaseSpec, draft);
        context.Pipeline.Set(ContextKeys.Plan, planFactory.Build(draft));
        logger.LogInformation(
            "Phase spec {PhaseId} extracted from ticket {Ticket} ({Goal})",
            draft.PhaseId, context.Ticket.Id.Value, draft.Goal);
        return Task.FromResult(CommandResult.Ok(
            $"Phase spec {draft.PhaseId} validated: {draft.Goal}"));
    }
}
