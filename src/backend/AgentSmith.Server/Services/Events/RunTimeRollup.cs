using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0404: the run's wall-clock split, rolled up from the time its STEPS carry.
/// The roll-up is deliberately the sum of the steps and nothing else — the number
/// on the run and the numbers in the drawer are then the same measurement, so a
/// disagreement between them is impossible rather than merely unlikely.
/// <para>
/// Time spent BETWEEN steps is therefore not counted here; it is neither model
/// nor sandbox nor any step's scaffolding, and inventing a home for it would make
/// the run total stop matching the rail the operator reads it against.
/// </para>
/// </summary>
public static class RunTimeRollup
{
    /// <summary>
    /// Null when no step carries attributed time — a pre-p0404 run has no split
    /// to show, and zeros would read as "the model did nothing".
    /// </summary>
    public static RunTimeSplitView? From(IEnumerable<RunStep> steps)
    {
        var rows = steps as IReadOnlyCollection<RunStep> ?? steps.ToList();
        var model = rows.Sum(s => s.LlmMs);
        var sandbox = rows.Sum(s => s.SandboxMs);
        if (model == 0 && sandbox == 0) return null;
        return new RunTimeSplitView(
            model,
            rows.Sum(s => s.ThrottleWaitMs),
            sandbox,
            rows.Sum(s => Scaffolding(s)));
    }

    // A step still running has no duration to subtract from, so it contributes no
    // scaffolding rather than a negative or a guess.
    private static long Scaffolding(RunStep step) =>
        RunTimeSplitView.From(step.LlmMs, step.ThrottleWaitMs, step.SandboxMs, step.DurationSeconds)
            .ScaffoldingMs ?? 0L;
}
