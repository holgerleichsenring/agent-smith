using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: makes one spec of the sequence the CURRENT one. Everything downstream —
/// the planner, the master prompt, the acceptance contract, VerifySpec and the spec
/// record — reads <see cref="ContextKeys.CurrentSpec"/>, so selecting a spec is the
/// only wiring the sequence needs and no handler learns that sequences exist.
/// <para>
/// p0460: and it is where the delivery account is taken FIRST. A spec whose ratified
/// criteria the branch already satisfies is done — it is recorded and the sequence moves
/// on, without a master pass and without asking the operator a question whose answer is
/// always the same.
/// </para>
/// </summary>
public sealed class SelectSpecHandler(
    SpecEntryAccount entryAccount,
    SpecStartHeads startHeads,
    ISpecProgressRecorder progress,
    ILogger<SelectSpecHandler> logger)
    : ICommandHandler<SelectSpecContext>
{
    /// <summary>What the sequence's per-spec table says about a spec that was found
    /// through before it started. Public because it IS the spec's recorded standing —
    /// the pull request renders it and a reader has to be able to tell it apart from a
    /// spec that ran.</summary>
    public const string AlreadySatisfiedNote = "already satisfied by the branch on entry";

    public async Task<CommandResult> ExecuteAsync(
        SelectSpecContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return CommandResult.Fail(
                "SelectSpec ran without a spec set — the sequence cannot name its spec.");

        var spec = set.Specs.FirstOrDefault(p => p.SpecId == context.SpecId);
        if (spec is null)
            return CommandResult.Fail($"Spec {context.SpecId} is not in spec set {set.Key}.");

        // p0394a: the spec IS the plan — publishing the draft is all the handover the
        // master needs; its plan section and ledger seed both read this key.
        context.Pipeline.Set(ContextKeys.CurrentSpec, spec.Draft);
        // p0444: a repair belongs to the spec that earned it. Entering a spec is where
        // the previous one's repair state ends — both the criteria it was closing and the
        // flag saying its single repair is spent.
        SpecRepairScope.Reset(context.Pipeline);
        SpecRunCommandScope.Reset(context.Pipeline);
        // 2026-09-17-042eh: and where the spec BEGINS, so its review reads its own diff.
        await startHeads.RecordAsync(context.Pipeline, cancellationToken);
        await progress.RecordAsync(
            context.Pipeline, spec.SpecId, SpecRunState.InProgress, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Spec {SpecId} of spec set {Key} is now current: {Goal}",
            spec.SpecId, set.Key, spec.Draft.Goal);

        var accounts = await entryAccount.TakeAsync(context.Pipeline, cancellationToken);
        return Satisfied(accounts)
            ? await AlreadyDoneAsync(context.Pipeline, spec, accounts, cancellationToken)
            : CommandResult.Ok($"Spec {spec.SpecId}: {spec.Draft.Goal}");
    }

    /// <summary>
    /// Every criterion of every repository, satisfied. An account that could not be taken
    /// carries a problem and is therefore not satisfied, and one outstanding criterion is
    /// enough to work the spec — p0438 hands that list back to the master at the end,
    /// which is where a partial delivery is finished.
    /// </summary>
    private static bool Satisfied(IReadOnlyList<SpecAccount> accounts) =>
        accounts.Count > 0 && accounts.All(a => a.Delivered);

    /// <summary>
    /// The spec is through before it started. It is RECORDED as through — in the run's
    /// step result, in the sequence's per-spec table, in the run's account ledger and,
    /// via WriteSpecRecord, on the branch itself. A silently skipped spec would be
    /// indistinguishable from a spec that was never run.
    /// </summary>
    private async Task<CommandResult> AlreadyDoneAsync(
        PipelineContext pipeline, DerivedSpec spec, IReadOnlyList<SpecAccount> accounts,
        CancellationToken cancellationToken)
    {
        pipeline.Set(ContextKeys.SpecAccounts, accounts);
        RunAccountLedger.Record(pipeline, accounts);
        await progress.RecordAsync(
            pipeline, spec.SpecId, SpecRunState.Done, note: AlreadySatisfiedNote,
            cancellationToken: cancellationToken);

        var message = $"Spec {spec.SpecId} is already satisfied by the branch — "
            + $"{Describe(accounts)}. No work is needed; the sequence moves on.";
        logger.LogInformation("{Message}", message);
        return CommandResult.OkAndDropAhead(message, PipelinePresets.SpecWorkSteps);
    }

    /// <summary>What satisfied it, in the account's own citations — an operator reading a
    /// spec that produced no work must see WHY it produced none.</summary>
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

}
