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
            // p0393: the gate now sits in the ONE code-changing preset, which runs ordinary
            // tickets too — and an ordinary ticket carries no spec. Failing here would break
            // every bug and feature ticket between this phase and p0393a, which is the phase
            // that DERIVES a spec so there is always one. So a ticket without a spec passes
            // through and the run plans from the ticket exactly as it did before; a ticket
            // that carries a MALFORMED spec still fails, because that is someone trying to
            // ship a spec and getting it wrong, which must not degrade silently.
            if (invalid.IsAbsent)
            {
                logger.LogInformation(
                    "Ticket {Ticket} carries no phase spec — planning from the ticket "
                    + "(p0393a derives one)", context.Ticket.Id.Value);
                return Task.FromResult(CommandResult.Ok(
                    "No phase spec on the ticket; planning from the ticket"));
            }

            logger.LogWarning(
                "Ticket {Ticket} carries a malformed phase spec: {Error}",
                context.Ticket.Id.Value, invalid.Error);
            return Task.FromResult(CommandResult.Fail(
                $"Ticket {context.Ticket.Id.Value} carries a malformed phase spec: {invalid.Error}"));
        }

        var draft = ((PhaseSpecExtracted)extraction).Draft;
        context.Pipeline.Set(ContextKeys.PhaseSpec, draft);
        // p0393: the gate publishes the SPEC and stops there. p0315d could publish it as
        // the approved plan because its tickets were authored by the operator in-thread,
        // where the planning had already happened; a spec states WHAT is expected, and with
        // no code samples there is no plan inside it. GeneratePlan derives the plan from
        // this spec against the actual codebase — the step a developer performs before
        // writing a line.
        logger.LogInformation(
            "Phase spec {PhaseId} extracted from ticket {Ticket} ({Goal})",
            draft.PhaseId, context.Ticket.Id.Value, draft.Goal);
        return Task.FromResult(CommandResult.Ok(
            $"Phase spec {draft.PhaseId} validated: {draft.Goal}"));
    }
}
