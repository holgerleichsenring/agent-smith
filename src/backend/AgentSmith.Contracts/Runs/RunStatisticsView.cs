namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: everything the story view reads about one run — the ticket's totals, its phases,
/// and the two series it draws: the calls in call order and the commands with their exit
/// codes.
/// <para>
/// It is a QUERY, produced by <see cref="RunStatisticsFold"/> over the recorded trail. The
/// run row grows no columns for it, so nothing here can drift from the events it describes.
/// </para>
/// </summary>
/// <param name="Truncated">True when the run produced more points than one response carries;
/// the LATEST ones are served, because the end of a run is where the shape gets interesting.</param>
public sealed record RunStatisticsView(
    RunCallStatistics Totals,
    long TotalDurationMs,
    IReadOnlyList<RunPhaseStatistics> Phases,
    IReadOnlyList<RunCallPoint> Calls,
    IReadOnlyList<RunCommandPoint> Commands,
    bool Truncated,
    RunWorkBreakdown Work)
{
    public static RunStatisticsView Empty { get; } =
        new(RunCallStatistics.From([]), 0, [], [], [], false, RunWorkBreakdown.Empty);
}
