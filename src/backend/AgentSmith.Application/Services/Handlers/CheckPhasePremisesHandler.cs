using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-17-0e79c: checks the premises the CURRENT phase states before its work starts, and
/// hands a false one back instead of building on it.
/// <para>
/// Nothing checked a premise before. Facts are resolved once, at derivation, and afterwards
/// only rendered — so an approved set, a branch artifact or an embedded spec was never looked
/// at again. This is the answer to that staleness, and it must not become a second deriver: it
/// REPORTS, and the amendment is made where a change to the specification is made.
/// </para>
/// <para>
/// It runs after SelectPhase and is therefore a phase WORK step: a phase the branch already
/// satisfies drops it with the rest of the work (<see cref="PipelinePresets.PhaseWorkSteps"/>),
/// because a phase that needs no work needs no premise check.
/// </para>
/// </summary>
public sealed class CheckPhasePremisesHandler(
    IPhasePremiseChecker checker,
    DerivationLookFactory looks,
    IPhaseProgressRecorder progress,
    PremiseHandbackNotice notice,
    ILogger<CheckPhasePremisesHandler> logger)
    : ICommandHandler<CheckPhasePremisesContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CheckPhasePremisesContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) || draft is null)
            return Skipped("no phase is current, so there are no premises to check");

        var premises = PhasePremises.For(draft);
        // Nothing back-fills premises: a spec drafted before the design partner stated them is
        // checked against nothing. The skip is recorded so an operator can tell it from a pass.
        // Nothing back-fills premises, and the deriver's own boilerplate decision is not one
        // (DerivedPhaseDecision) — so a derived phase that states nothing of its own lands here
        // and costs no call at all.
        if (premises.None)
            return Skipped($"phase {draft.PhaseId} states no premises of its own");
        if (PipelineCostTracker.GetOrCreate(context.Pipeline).IsBudgetExhausted)
            return Skipped($"the run's cost cap is exhausted before phase {draft.PhaseId}");

        await using var look = looks.ForPremiseCheck(context.Pipeline);
        // Every verdict this check can reach rests on a look that ran, so with no repository to
        // look into there is no question worth paying for.
        if (look is null) return Skipped("the run has no repository sandbox to check against");

        var check = await checker.CheckAsync(
            draft, premises, look, PrecedingPhases.Of(context.Pipeline, draft.PhaseId),
            draft.PhaseId, context.AgentConfig,
            PipelineCostTracker.GetOrCreate(context.Pipeline), cancellationToken);
        return check.False.Count == 0
            ? Held(draft, check)
            : await HandBackAsync(context, draft, check, cancellationToken);
    }

    /// <summary>The question was put and produced no answer. Failing open is right; calling it
    /// a skip is not — an operator must be able to tell it from a decision not to ask.</summary>
    private CommandResult NotTaken(string reason)
    {
        var message = $"The premise check could not be taken: {reason}. The phase runs unchecked.";
        logger.LogWarning("{Message}", message);
        return CommandResult.Ok(message);
    }

    private CommandResult Skipped(string reason)
    {
        var message = $"Premise check skipped: {reason}.";
        logger.LogInformation("{Message}", message);
        return CommandResult.Ok(message);
    }

    private CommandResult Held(PhaseDraft draft, PremiseCheck check)
    {
        if (check.Skipped is not null) return Skipped(check.Skipped);
        if (check.NotTaken is not null) return NotTaken(check.NotTaken);
        var message = $"Phase {draft.PhaseId}: every stated premise still holds"
            + (check.Unproven.Count == 0
                ? "."
                : $"; {check.Unproven.Count} reported premise(s) could not be proven either way "
                  + $"and stop nothing: {string.Join("; ", check.Unproven.Select(u => u.Premise))}");
        logger.LogInformation("{Message}", message);
        return CommandResult.Ok(message);
    }

    /// <summary>
    /// The hand-back. The phase row is written at SelectPhase and at verification, so a step
    /// failing between them would leave the row "in progress" forever unless this records it.
    /// The premise, the finding and its evidence go as ONE argument, because the recorder
    /// publishes failingCommand ?? note and a second argument would be dropped silently.
    /// <para>
    /// The standing is <see cref="PhaseRunState.HandedBack"/>, never <c>Failed</c>: a red build
    /// is fixed by working the code and a false premise by amending the specification, and a
    /// reader that had to tell the two apart by parsing this verdict string would be deriving
    /// what the producer already knows.
    /// </para>
    /// </summary>
    private async Task<CommandResult> HandBackAsync(
        CheckPhasePremisesContext context, PhaseDraft draft, PremiseCheck check, CancellationToken ct)
    {
        var verdict = string.Join(
            " | ", check.False.Select(f => PremiseHandback.Verdict(draft.PhaseId, f)));
        await progress.RecordAsync(
            context.Pipeline, draft.PhaseId, PhaseRunState.HandedBack, verdict, cancellationToken: ct);
        await notice.PostAsync(context.Pipeline, context.Tracker, draft, check.False, ct);
        logger.LogWarning("{Verdict}", verdict);
        return CommandResult.Fail(verdict);
    }
}
