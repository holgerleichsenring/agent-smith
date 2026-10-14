using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: splices one master → verify → record block into the pipeline per derived spec, in order. The
/// sequence is where TERMINATION comes from: each spec ends at its own done-list and VerifySpec, so stopping is
/// structural. A red VerifySpec fails its step and leaves the remaining blocks unexecuted — the finalizer tail
/// still runs, so the half-migrated state reaches the pull request instead of disappearing with the run.
/// </summary>
public sealed class SpecSequenceHandler(
    Specs.ISeriesSettingsReader series, TimeProvider clock, ILogger<SpecSequenceHandler> logger)
    : ICommandHandler<SpecSequenceContext>
{
    // The block lives on PipelinePresets, not here: the park-capability questions are
    // asked against the preset at CONFIGURATION time, before any run splices anything.
    private static IReadOnlyList<string> PerSpec => PipelinePresets.CodeSpecBlock;

    public Task<CommandResult> ExecuteAsync(
        SpecSequenceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null || set.Specs.Count == 0)
            return Task.FromResult(CommandResult.Ok("No derived specs to execute — the run has no spec set"));

        var pending = set.UnexecutedTail;
        if (pending.Count == 0)
            return Task.FromResult(CommandResult.Ok(
                $"Every spec of {set.Key} already executed on this branch — nothing left to run"));

        // 2026-10-13-8762u: one attempt runs a chunk sized by series.specs_per_attempt and the wall clock LEFT.
        var (size, refused) = AttemptSize(context.Pipeline);
        if (refused is not null) return Task.FromResult(CommandResult.Fail(refused));
        // 2026-10-13-8762g: a stop splices only the specs before it; the rest is left to the next attempt.
        var stop = context.Pipeline.TryGet<SpecSequenceStop>(ContextKeys.SpecSequenceStop, out var s) ? s : null;
        var beforeStop = stop is null ? pending.Count : pending.TakeWhile(p => p.SpecId != stop.BeforeSpecId).Count();
        IReadOnlyList<DerivedSpec> building = [.. pending.Take(Math.Min(size, beforeStop))];
        var chunked = building.Count < beforeStop; // a Park stop beyond the chunk end is not applied this attempt
        context.Pipeline.Set(ContextKeys.SpecSequenceProgress, Progress(set, pending, building.Count, beforeStop, stop));
        // 2026-09-17-0e79e: the executor's loop guard is sized here, because this is where
        // the work is known — the specs this attempt splices, never the set's own count.
        context.Pipeline.Set(ContextKeys.StepBudget, StepBudget.ForSpecs(building.Count));
        var commands = building.SelectMany(spec => PerSpec.Select(name => new PipelineCommand(name) { SpecId = spec.SpecId }))
            .Concat(stop is { Park: true } && !chunked ? [PipelineCommand.Simple(CommandNames.SpecHandback)] : [])
            .ToArray();

        var next = chunked ? $"; this attempt's chunk is {building.Count} of {pending.Count}; the next attempt starts at {pending[building.Count].SpecId}" : string.Empty;
        logger.LogInformation(
            "Spec set {Key}: {Pending} spec(s) to execute — {Steps} spliced steps ({Specs}){Next}",
            set.Key, building.Count, commands.Length, string.Join(", ", building.Select(p => p.SpecId)), next);
        return Task.FromResult(CommandResult.OkAndContinueWith(
            $"{building.Count} spec(s) to work through: "
            + string.Join(", ", building.Select(p => $"{p.SpecId} ({p.Draft.Goal})"))
            + (stop is null || chunked ? string.Empty : $"; stopping before {stop.BeforeSpecId}: {stop.Reason}") + next,
            commands));
    }

    /// <summary>The specs this attempt may run, or why not even one fits — never floored to one.</summary>
    private (int Size, string? Refused) AttemptSize(PipelineContext pipeline)
    {
        var settings = series.Current();
        if (settings.Refusal() is { } invalid) return (0, invalid);
        var left = settings.SecondsLeft(pipeline, clock.GetUtcNow());
        var size = left is { } l ? settings.Series.AttemptSize(l) : settings.Series.SpecsPerAttempt;
        return size > 0 ? (size, null) : (0, $"No spec block fits the time left: series.seconds_per_spec is "
            + $"{settings.Series.SecondsPerSpec}, {left} s are left of orchestrator.max_run_wall_time_seconds "
            + $"({settings.RunWallSeconds}), and one block is reserved for the commit-and-PR tail");
    }

    /// <summary>The progress table: the specs past the chunk deferred with the chunk's reason up to g's stop and
    /// with the stop's from it; when g's stop ends the attempt, a parked stop spec is handed back with its reason.</summary>
    private static SpecSequenceProgress Progress(
        SpecSet set, IReadOnlyList<DerivedSpec> pending, int built, int beforeStop, SpecSequenceStop? stop)
    {
        var progress = SpecSequenceProgress.ForSet(set);
        var after = pending.Skip(built).Select(p => p.SpecId).ToList();
        if (after.Count == 0) return progress;
        if (built < beforeStop)
        {
            var chunkPart = stop is { Park: false } ? beforeStop - built : after.Count;
            progress = progress.Deferring(after.Take(chunkPart).ToList(),
                $"beyond this attempt's chunk of {built} spec(s) (series.specs_per_attempt and the wall clock left)");
            return chunkPart < after.Count ? progress.Deferring(after.Skip(chunkPart).ToList(), stop!.Reason) : progress;
        }
        var deferred = after.Where(id => !stop!.Park || id != stop.BeforeSpecId).ToList();
        progress = progress.Deferring(deferred, stop!.Reason);
        return stop.Park ? progress.With(stop.BeforeSpecId, SpecRunState.HandedBack, stop.Reason) : progress;
    }
}
