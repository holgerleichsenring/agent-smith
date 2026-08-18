using AgentSmith.Contracts.Events;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: the ticket's statistics, folded out of the recorded trail — phases, their
/// durations, the calls inside them and their sizes.
/// <para>
/// Group first, fold second: the same <see cref="RunCallStatistics"/> that answers for a
/// run answers for a phase when it is handed that phase's events. Nothing here is counted
/// while the run happens, so nothing here can disagree with the run.
/// </para>
/// </summary>
public static class RunStatisticsFold
{
    /// <summary>How many points of each series one response carries; the LATEST are kept.</summary>
    public const int MaxPoints = 2000;

    public static RunStatisticsView Fold(
        IReadOnlyList<RunEvent> events, IReadOnlyList<RunStepFacts> steps, int maxPoints = MaxPoints)
    {
        var phaseByStep = steps
            .GroupBy(s => s.StepIndex)
            .ToDictionary(g => g.Key, g => g.First().PhaseId);
        string? PhaseOf(int? stepIndex) =>
            stepIndex is { } i && phaseByStep.TryGetValue(i, out var phase) ? phase : null;

        var calls = RunWorkPoints.Calls(events, PhaseOf);
        var commands = RunWorkPoints.Commands(events, PhaseOf);
        return new RunStatisticsView(
            RunCallStatistics.From(events),
            steps.Sum(s => s.DurationMs),
            Phases(events, steps, PhaseOf),
            Tail(calls, maxPoints),
            Tail(commands, maxPoints),
            calls.Count > maxPoints || commands.Count > maxPoints,
            Breakdown(steps, commands));
    }

    // The phases in the order the run ran them — a phase's place is the first step that
    // carries it, so the sequence is the run's own, not the alphabet's.
    private static IReadOnlyList<RunPhaseStatistics> Phases(
        IReadOnlyList<RunEvent> events, IReadOnlyList<RunStepFacts> steps, Func<int?, string?> phaseOf)
    {
        var byPhase = events.ToLookup(e => phaseOf(e.OriginStepIndex));
        return
        [
            .. steps
                .GroupBy(s => s.PhaseId)
                .OrderBy(g => g.Min(s => s.StepIndex))
                .Select(g => Compose(g.Key, [.. g], byPhase[g.Key]))
        ];
    }

    private static RunPhaseStatistics Compose(
        string? phaseId, IReadOnlyList<RunStepFacts> steps, IEnumerable<RunEvent> events)
    {
        var own = events.ToList();
        var commands = own.OfType<SandboxResultEvent>().ToList();
        return new RunPhaseStatistics(
            phaseId,
            steps.Count,
            steps.Sum(s => s.DurationMs),
            RunCallStatistics.From(own),
            commands.Count,
            commands.Count(c => c.ExitCode != 0));
    }

    // The end of a run is where the shape gets interesting, so an over-long series keeps
    // its tail. Index stays absolute, so a truncated plot still says where it starts.
    private static IReadOnlyList<T> Tail<T>(IReadOnlyList<T> points, int max) =>
        points.Count <= max ? points : [.. points.Skip(points.Count - max)];

    // p0341h: what the run spent its time ON. Pipeline rows come from the steps (the run's
    // own plan); sandbox rows from the commands those steps issued. Both are grouped by the
    // name the producer already recorded — no parsing, so nothing here can misread a shell
    // line it was never meant to understand.
    private static RunWorkBreakdown Breakdown(
        IReadOnlyList<RunStepFacts> steps, IReadOnlyList<RunCommandPoint> commands) =>
        new(
            [.. steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .GroupBy(s => s.Name!, StringComparer.Ordinal)
                .Select(g => new RunWorkKind(g.Key, g.Count(), g.Sum(s => s.DurationMs)))
                .OrderByDescending(k => k.DurationMs).ThenByDescending(k => k.Count)],
            [.. commands
                .GroupBy(c => string.IsNullOrWhiteSpace(c.Command) ? "(unnamed)" : c.Command,
                    StringComparer.Ordinal)
                .Select(g => new RunWorkKind(
                    g.Key, g.Count(), g.Sum(c => c.DurationMs), g.Count(c => c.ExitCode != 0)))
                .OrderByDescending(k => k.DurationMs).ThenByDescending(k => k.Count)]);
}
