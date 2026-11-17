using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-17-0e79c: checks the premises the CURRENT spec states before its work starts, and
/// hands a false one back instead of building on it.
/// <para>
/// Nothing checked a premise before. Facts are resolved once, at derivation, and afterwards
/// only rendered — so an approved set, a branch artifact or an embedded spec was never looked
/// at again. This is the answer to that staleness, and it must not become a second deriver: it
/// REPORTS, and the amendment is made where a change to the specification is made.
/// </para>
/// <para>
/// It runs after SelectSpec and is therefore a spec WORK step: a spec the branch already
/// satisfies drops it with the rest of the work (<see cref="PipelinePresets.SpecWorkSteps"/>),
/// because a spec that needs no work needs no premise check.
/// </para>
/// <para>2026-11-17-c47ea: an approved spec's false premise breaking no criterion is a <see cref="PremiseCorrection"/>.</para>
/// </summary>
public sealed class CheckSpecPremisesHandler(
    ISpecPremiseChecker checker,
    DerivationLookFactory looks,
    ISpecProgressRecorder progress,
    PremiseHandbackNotice notice,
    PremiseSupersessionFilter supersession, // 2026-11-04-6768a: a sibling's own work is no false premise
    PremiseCorrection correction, // 2026-11-17-c47ea: replaced at every spec entry
    ILogger<CheckSpecPremisesHandler> logger)
    : ICommandHandler<CheckSpecPremisesContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CheckSpecPremisesContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Pipeline.Set<IReadOnlyList<string>>(ContextKeys.PremiseCorrections, []);
        if (!context.Pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) || draft is null)
            return Skipped("no spec is current, so there are no premises to check");

        var premises = SpecPremises.For(draft);
        // Nothing back-fills premises, and the deriver's own boilerplate decision is not one
        // (DerivedSpecDecision): a spec stating nothing of its own costs no call, and the skip is
        // recorded so an operator can tell it from a pass.
        if (premises.None)
            return Skipped($"spec {draft.SpecId} states no premises of its own");
        if (PipelineCostTracker.GetOrCreate(context.Pipeline) is { IsBudgetExhausted: true } capped)
            return Skipped($"{CostCapStop.Describe(capped)} — spec {draft.SpecId} is UNCHECKED");

        await using var look = looks.ForPremiseCheck(context.Pipeline);
        // Every verdict this check can reach rests on a look that ran, so with no repository to
        // look into there is no question worth paying for.
        if (look is null) return Skipped("the run has no repository sandbox to check against");

        var check = await checker.CheckAsync(
            draft, premises, look, PrecedingSpecs.Of(context.Pipeline, draft.SpecId),
            PrecedingSpecs.NotesOf(context.Pipeline, draft.SpecId), // 2026-10-13-8762m
            draft.SpecId, context.AgentConfig,
            PipelineCostTracker.GetOrCreate(context.Pipeline), cancellationToken);
        var sorted = await supersession.SortAsync(context.Pipeline, draft, check.False, look, cancellationToken);
        return sorted.StillFalse.Count == 0 ? Held(draft, check, sorted.Superseded)
            : PremiseCorrection.Applies(context.Pipeline, sorted.StillFalse)
            ? CommandResult.Ok(await correction.ApplyAsync(context.Pipeline, draft, sorted.StillFalse, cancellationToken))
            : await HandBackAsync(context, draft, check with { False = sorted.StillFalse }, cancellationToken);
    }

    /// <summary>No answer came back: failing open is right, calling it a skip is not.</summary>
    private CommandResult NotTaken(string reason)
    {
        var message = $"The premise check could not be taken: {reason}. The spec runs unchecked.";
        logger.LogWarning("{Message}", message);
        return CommandResult.Ok(message);
    }

    private CommandResult Skipped(string reason)
    {
        var message = $"Premise check skipped: {reason}.";
        logger.LogInformation("{Message}", message);
        return CommandResult.Ok(message);
    }

    private CommandResult Held(SpecDraft draft, PremiseCheck check, string? superseded)
    {
        if (check.Skipped is not null) return Skipped(check.Skipped);
        if (check.NotTaken is not null) return NotTaken(check.NotTaken);
        var message = (superseded ?? $"Spec {draft.SpecId}: every stated premise still holds")
            + (check.Unproven.Count == 0
                ? "."
                : $"; {check.Unproven.Count} reported premise(s) could not be proven either way "
                  + $"and stop nothing: {string.Join("; ", check.Unproven.Select(u => u.Premise))}");
        logger.LogInformation("{Message}", message);
        return CommandResult.Ok(message);
    }

    /// <summary>
    /// The hand-back. The spec row is written at SelectSpec and at verification, so a step
    /// failing between them would leave the row "in progress" forever unless this records it.
    /// The premise, the finding and its evidence go as ONE argument, because the recorder
    /// publishes failingCommand ?? note and a second argument would be dropped silently.
    /// <para>
    /// The standing is <see cref="SpecRunState.HandedBack"/>, never <c>Failed</c>: a red build is fixed by working the
    /// code, a false premise by amending the specification, and no reader should parse the verdict to tell them apart.
    /// </para>
    /// </summary>
    private async Task<CommandResult> HandBackAsync(
        CheckSpecPremisesContext context, SpecDraft draft, PremiseCheck check, CancellationToken ct)
    {
        var verdict = string.Join(
            " | ", check.False.Select(f => PremiseHandback.Verdict(draft.SpecId, f)));
        await progress.RecordAsync(
            context.Pipeline, draft.SpecId, SpecRunState.HandedBack, verdict, cancellationToken: ct);
        await notice.PostAsync(context.Pipeline, context.Tracker, draft, check.False, ct);
        logger.LogWarning("{Verdict}", verdict);
        return CommandResult.Fail(verdict);
    }
}
