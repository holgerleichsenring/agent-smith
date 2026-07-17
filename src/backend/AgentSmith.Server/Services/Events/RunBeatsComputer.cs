using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0344b: derives the five run-story beat states SERVER-side from the run's
/// typed command progress — the persisted RunStep rows carry the command name
/// (StepStartedEvent.CommandName) and <see cref="CommandBeats"/> maps each
/// command type to its beat. Never matches on display-label strings. A run
/// whose stored steps predate the typed command name cannot be mapped and
/// yields null — the client renders NO storybar instead of guessing (no
/// heuristic for missing data).
/// </summary>
public static class RunBeatsComputer
{
    public static RunBeatsView? Compute(Run run)
    {
        // One row per step index: StartStep inserts, FinishStep mutates the same
        // row, but a re-driven index can add rows — the latest row is the truth.
        var steps = run.Steps
            .GroupBy(s => s.StepIndex)
            .Select(g => g.OrderByDescending(s => s.Id).First())
            .OrderBy(s => s.StepIndex)
            .ToList();

        var mapped = steps
            .Where(s => s.CommandName is not null && CommandBeats.TryGet(s.CommandName, out _))
            .Select(s => { CommandBeats.TryGet(s.CommandName!, out var beat); return (Step: s, Beat: beat); })
            .ToList();

        var terminal = run.FinishedAt is not null;

        if (mapped.Count == 0)
        {
            // Steps exist but none carries a typed command name → pre-p0344b
            // data; a terminal run with no steps at all is equally unmappable.
            if (steps.Count > 0 || terminal) return null;
            // Admitted but not yet stepping (queued / launching): every beat the
            // pipeline DEFINITION contains is pending, the rest skipped. An
            // unknown pipeline promises nothing.
            var planned = PlannedBeats(run.Pipeline);
            return planned is null
                ? null
                : Render(beat => planned.Contains(beat) ? BeatStates.Pending : BeatStates.Skipped);
        }

        var currentBeat = mapped[^1].Beat;
        var plannedBeats = PlannedBeats(run.Pipeline)
            ?? mapped.Select(m => m.Beat).ToHashSet();
        var success = string.Equals(run.Status, "success", StringComparison.OrdinalIgnoreCase);

        return Render(beat => StateOf(beat, mapped, currentBeat, plannedBeats, terminal, success));
    }

    private static string StateOf(
        RunBeat beat,
        IReadOnlyList<(RunStep Step, RunBeat Beat)> mapped,
        RunBeat currentBeat,
        IReadOnlySet<RunBeat> plannedBeats,
        bool terminal,
        bool success)
    {
        var beatSteps = mapped.Where(m => m.Beat == beat).Select(m => m.Step).ToList();

        if (beatSteps.Count == 0)
        {
            if (!plannedBeats.Contains(beat)) return BeatStates.Skipped;
            if (!terminal) return BeatStates.Pending;
            // Terminal: a SUCCESSFUL run that emitted no step of a planned beat
            // legitimately skipped it (short-circuit / dynamic list); a failed or
            // cancelled run stopped before reaching it → pending, the honest
            // "the story ended before this". Execution order can differ from the
            // canonical beat order, so position is not used here.
            return success ? BeatStates.Skipped : BeatStates.Pending;
        }

        var anyFailed = beatSteps.Any(s => s.Status == "failed");
        if (anyFailed) return BeatStates.Failed;

        var allSuccess = beatSteps.All(s => s.Status == "success");
        if (terminal)
            // A terminal non-success run with an unfinished step (crash/cancel
            // mid-step) died in this beat — that IS its failure point.
            return allSuccess || success ? BeatStates.Done : BeatStates.Failed;

        if (beat == currentBeat) return BeatStates.Active;
        return allSuccess ? BeatStates.Done : BeatStates.Active;
    }

    // The beats the pipeline DEFINITION contains — drives "skipped" (a preset
    // with no verify-beat command renders verify as skipped, not pending). Null
    // for pipelines without a code-defined preset: we then only know what ran.
    private static IReadOnlySet<RunBeat>? PlannedBeats(string pipeline)
    {
        var commands = PipelinePresets.TryResolve(pipeline);
        if (commands is null) return null;
        var beats = new HashSet<RunBeat>();
        foreach (var command in commands)
            if (CommandBeats.TryGet(command, out var beat))
                beats.Add(beat);
        return beats;
    }

    private static RunBeatsView Render(Func<RunBeat, string> state) => new(
        Ticket: state(RunBeat.Ticket),
        Plan: state(RunBeat.Plan),
        Building: state(RunBeat.Building),
        Verify: state(RunBeat.Verify),
        Outcome: state(RunBeat.Outcome));
}
