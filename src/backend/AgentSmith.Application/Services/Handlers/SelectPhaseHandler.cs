using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
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
/// <para>
/// p0460: and it is where the delivery account is taken FIRST. A phase whose ratified
/// criteria the branch already satisfies is done — it is recorded and the sequence moves
/// on, without a master pass and without asking the operator a question whose answer is
/// always the same.
/// </para>
/// </summary>
public sealed class SelectPhaseHandler(
    PhaseEntryAccount entryAccount,
    ILogger<SelectPhaseHandler> logger)
    : ICommandHandler<SelectPhaseContext>
{
    /// <summary>What the sequence's per-phase table says about a phase that was found
    /// through before it started. Public because it IS the phase's recorded standing —
    /// the pull request renders it and a reader has to be able to tell it apart from a
    /// phase that ran.</summary>
    public const string AlreadySatisfiedNote = "already satisfied by the branch on entry";

    public async Task<CommandResult> ExecuteAsync(
        SelectPhaseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return CommandResult.Fail(
                "SelectPhase ran without a spec set — the sequence cannot name its phase.");

        var phase = set.Phases.FirstOrDefault(p => p.PhaseId == context.PhaseId);
        if (phase is null)
            return CommandResult.Fail($"Phase {context.PhaseId} is not in spec set {set.Key}.");

        // p0394a: the spec IS the plan — publishing the draft is all the handover the
        // master needs; its plan section and ledger seed both read this key.
        context.Pipeline.Set(ContextKeys.PhaseSpec, phase.Draft);
        // p0444: a repair belongs to the phase that earned it. Entering a phase is where
        // the previous one's repair state ends — both the criteria it was closing and the
        // flag saying its single repair is spent.
        PhaseRepairScope.Reset(context.Pipeline);
        Record(context.Pipeline, phase, set, PhaseRunState.InProgress);

        logger.LogInformation(
            "Phase {PhaseId} of spec set {Key} is now current: {Goal}",
            phase.PhaseId, set.Key, phase.Draft.Goal);

        var accounts = await entryAccount.TakeAsync(context.Pipeline, cancellationToken);
        return Satisfied(accounts)
            ? AlreadyDone(context.Pipeline, phase, set, accounts)
            : CommandResult.Ok($"Phase {phase.PhaseId}: {phase.Draft.Goal}");
    }

    /// <summary>
    /// Every criterion of every repository, satisfied. An account that could not be taken
    /// carries a problem and is therefore not satisfied, and one outstanding criterion is
    /// enough to work the phase — p0438 hands that list back to the master at the end,
    /// which is where a partial delivery is finished.
    /// </summary>
    private static bool Satisfied(IReadOnlyList<SpecAccount> accounts) =>
        accounts.Count > 0 && accounts.All(a => a.Delivered);

    /// <summary>
    /// The phase is through before it started. It is RECORDED as through — in the run's
    /// step result, in the sequence's per-phase table, in the run's account ledger and,
    /// via WritePhaseRecord, on the branch itself. A silently skipped phase would be
    /// indistinguishable from a phase that was never run.
    /// </summary>
    private CommandResult AlreadyDone(
        PipelineContext pipeline, SpecPhase phase, SpecSet set, IReadOnlyList<SpecAccount> accounts)
    {
        pipeline.Set(ContextKeys.PhaseAccounts, accounts);
        RunAccountLedger.Record(pipeline, accounts);
        Record(pipeline, phase, set, PhaseRunState.Done, AlreadySatisfiedNote);

        var message = $"Phase {phase.PhaseId} is already satisfied by the branch — "
            + $"{Describe(accounts)}. No work is needed; the sequence moves on.";
        logger.LogInformation("{Message}", message);
        return CommandResult.OkAndDropAhead(message, PipelinePresets.PhaseWorkSteps);
    }

    /// <summary>What satisfied it, in the account's own citations — an operator reading a
    /// phase that produced no work must see WHY it produced none.</summary>
    private static string Describe(IReadOnlyList<SpecAccount> accounts)
    {
        var criteria = accounts.SelectMany(a => a.Criteria).ToList();
        var citations = criteria
            .Select(c => c.Citation)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();
        return $"all {criteria.Count} ratified criterion(s) are accounted for"
            + (citations.Count > 0 ? $" ({string.Join(", ", citations)})" : string.Empty);
    }

    // The phase before this one finished its VerifyPhase to get here, so entering a phase
    // is also the moment the previous one is provably done.
    private static void Record(
        PipelineContext pipeline, SpecPhase phase, SpecSet set,
        PhaseRunState state, string? note = null)
    {
        var progress = pipeline.TryGet<SpecSequenceProgress>(
            ContextKeys.SpecSequenceProgress, out var p) && p is not null
            ? p : SpecSequenceProgress.ForSet(set);
        pipeline.Set(
            ContextKeys.SpecSequenceProgress, progress.With(phase.PhaseId, state, note: note));
    }
}
