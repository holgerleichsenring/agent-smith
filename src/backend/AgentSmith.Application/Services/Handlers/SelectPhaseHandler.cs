using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: makes one phase of the sequence the CURRENT one. Everything downstream —
/// the planner, the master prompt, the acceptance contract, VerifyPhase and the phase
/// record — reads <see cref="ContextKeys.PhaseSpec"/>, so selecting a phase is the
/// only wiring the sequence needs and no handler learns that sequences exist.
/// </summary>
public sealed class SelectPhaseHandler(ILogger<SelectPhaseHandler> logger)
    : ICommandHandler<SelectPhaseContext>
{
    public Task<CommandResult> ExecuteAsync(
        SelectPhaseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return Task.FromResult(CommandResult.Fail(
                "SelectPhase ran without a spec set — the sequence cannot name its phase."));

        var phase = set.Phases.FirstOrDefault(p => p.PhaseId == context.PhaseId);
        if (phase is null)
            return Task.FromResult(CommandResult.Fail(
                $"Phase {context.PhaseId} is not in spec set {set.Key}."));

        // p0394a: the spec IS the plan — publishing the draft is all the handover the
        // master needs; its plan section and ledger seed both read this key.
        context.Pipeline.Set(ContextKeys.PhaseSpec, phase.Draft);
        // p0444: a repair belongs to the phase that earned it. Entering a phase is where
        // the previous one's repair state ends — both the criteria it was closing and the
        // flag saying its single repair is spent.
        PhaseRepairScope.Reset(context.Pipeline);
        Advance(context.Pipeline, phase, set);

        logger.LogInformation(
            "Phase {PhaseId} of spec set {Key} is now current: {Goal}",
            phase.PhaseId, set.Key, phase.Draft.Goal);
        return Task.FromResult(CommandResult.Ok(
            $"Phase {phase.PhaseId}: {phase.Draft.Goal}"));
    }

    // The phase before this one finished its VerifyPhase to get here, so entering a phase
    // is also the moment the previous one is provably done.
    private static void Advance(PipelineContext pipeline, SpecPhase phase, SpecSet set)
    {
        var progress = pipeline.TryGet<SpecSequenceProgress>(
            ContextKeys.SpecSequenceProgress, out var p) && p is not null
            ? p : SpecSequenceProgress.ForSet(set);
        pipeline.Set(
            ContextKeys.SpecSequenceProgress,
            progress.With(phase.PhaseId, PhaseRunState.InProgress));
    }
}
