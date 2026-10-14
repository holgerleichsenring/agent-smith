using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: splices one master → verify → record block into the pipeline per
/// derived spec, in order.
/// <para>
/// The sequence is where TERMINATION comes from: each spec ends at its own done-list
/// and its own VerifySpec, so stopping is structural instead of a judgement the model
/// has to reach. A red VerifySpec fails its step, which stops the pipeline at that
/// spec and leaves the remaining blocks unexecuted — the executor's finalizer tail
/// still runs, so the half-migrated state reaches the pull request instead of
/// disappearing with the run.
/// </para>
/// </summary>
public sealed class SpecSequenceHandler(ILogger<SpecSequenceHandler> logger)
    : ICommandHandler<SpecSequenceContext>
{
    // The block lives on PipelinePresets, not here: the park-capability questions are
    // asked against the preset at CONFIGURATION time, before any run splices anything.
    private static IReadOnlyList<string> PerSpec => PipelinePresets.CodeSpecBlock;

    public Task<CommandResult> ExecuteAsync(
        SpecSequenceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set)
            || set is null || set.Specs.Count == 0)
            return Task.FromResult(CommandResult.Ok(
                "No derived specs to execute — the run has no spec set"));

        var pending = set.UnexecutedTail;
        if (pending.Count == 0)
            return Task.FromResult(CommandResult.Ok(
                $"Every spec of {set.Key} already executed on this branch — nothing left to run"));

        // 2026-10-13-8762g: a stop splices only the specs before it; the rest is left to the next attempt.
        var stop = context.Pipeline.TryGet<SpecSequenceStop>(ContextKeys.SpecSequenceStop, out var s) ? s : null;
        IReadOnlyList<DerivedSpec> building = stop is null ? pending : [.. pending.TakeWhile(p => p.SpecId != stop.BeforeSpecId)];
        context.Pipeline.Set(ContextKeys.SpecSequenceProgress, Progress(set, pending.Skip(building.Count).ToList(), stop));
        // 2026-09-17-0e79e: the executor's loop guard is sized here, because this is where
        // the work is known — the specs this attempt splices, never the set's own count.
        context.Pipeline.Set(ContextKeys.StepBudget, StepBudget.ForSpecs(building.Count));
        var commands = building
            .SelectMany(spec => PerSpec.Select(
                name => new PipelineCommand(name) { SpecId = spec.SpecId }))
            .Concat(stop is { Park: true } ? [PipelineCommand.Simple(CommandNames.SpecHandback)] : [])
            .ToArray();

        logger.LogInformation(
            "Spec set {Key}: {Pending} spec(s) to execute — {Steps} spliced steps ({Specs})",
            set.Key, building.Count, commands.Length, string.Join(", ", building.Select(p => p.SpecId)));
        return Task.FromResult(CommandResult.OkAndContinueWith(
            $"{building.Count} spec(s) to work through: "
            + string.Join(", ", building.Select(p => $"{p.SpecId} ({p.Draft.Goal})"))
            + (stop is null ? string.Empty : $"; stopping before {stop.BeforeSpecId}: {stop.Reason}"),
            commands));
    }

    /// <summary>The progress table: the specs after the stop deferred to the next attempt, a parked stop
    /// spec handed back with the stop's reason.</summary>
    private static SpecSequenceProgress Progress(SpecSet set, IReadOnlyList<DerivedSpec> after, SpecSequenceStop? stop)
    {
        var progress = SpecSequenceProgress.ForSet(set);
        if (stop is null || after.Count == 0) return progress;
        var deferred = after.Select(p => p.SpecId).Where(id => !stop.Park || id != stop.BeforeSpecId).ToList();
        progress = progress.Deferring(deferred, stop.Reason);
        return stop.Park ? progress.With(stop.BeforeSpecId, SpecRunState.HandedBack, stop.Reason) : progress;
    }
}
