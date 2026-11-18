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
/// 2026-11-18-c8e2c: no account is taken on entry. A spec already executed on the branch is not spliced at all
/// (SpecSet.UnexecutedTail); one whose work is already there is worked by its master, which finds it done.
/// </para>
/// </summary>
public sealed class SelectSpecHandler(
    SpecStartHeads startHeads,
    ISpecProgressRecorder progress,
    Specs.Review.ISpecEntryGrounding entryGrounding,
    ILogger<SelectSpecHandler> logger)
    : ICommandHandler<SelectSpecContext>
{
    /// <summary>The note a spec found satisfied on entry was recorded with before 2026-11-18-c8e2c; stored progress
    /// rows still carry it and the record and memory outcome read them.</summary>
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
        (spec, var stepNote) = await entryGrounding.GroundAsync(context.Pipeline, set, spec, cancellationToken);

        // p0394a: the spec IS the plan — publishing the draft is all the handover the
        // master needs; its plan section and ledger seed both read this key.
        context.Pipeline.Set(ContextKeys.CurrentSpec, spec.Draft);
        // p0444: a repair belongs to the spec that earned it; 2026-11-18-c8e2c: so does a master's report.
        SpecRepairScope.Reset(context.Pipeline);
        SpecRunCommandScope.Reset(context.Pipeline);
        // 2026-09-17-042eh: and where the spec BEGINS, so its diff reads from here.
        await startHeads.RecordAsync(context.Pipeline, cancellationToken);
        await progress.RecordAsync(
            context.Pipeline, spec.SpecId, SpecRunState.InProgress, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Spec {SpecId} of spec set {Key} is now current: {Goal}",
            spec.SpecId, set.Key, spec.Draft.Goal);
        return CommandResult.Ok($"Spec {spec.SpecId}: {spec.Draft.Goal}" + stepNote);
    }
}
