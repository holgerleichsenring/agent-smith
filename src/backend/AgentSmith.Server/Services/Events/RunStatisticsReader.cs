using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0423b: the story view's read model — the run's recorded trail joined to its step rail
/// and folded into specs, calls and commands.
/// <para>
/// The join is what the events cannot do alone: an event knows the step it was produced in,
/// the step name knows the spec. Everything after the join is
/// <see cref="RunStatisticsFold"/>, which counts nothing and derives everything.
/// </para>
/// </summary>
public sealed class RunStatisticsReader(TrailReader trail, RunStepsReader steps)
{
    public async Task<RunStatisticsView> ReadAsync(string runId, CancellationToken cancellationToken)
    {
        var events = await trail.ReadDbTrailTypedAsync(runId);
        var rail = await steps.ReadAsync(runId, cancellationToken);
        return RunStatisticsFold.Fold(events, [.. rail.Where(s => !s.Planned).Select(ToFacts)]);
    }

    // An announced-but-unreached step has no duration and no events; only what RAN counts.
    // p0341h: the OPERATOR-facing label is what the breakdown groups by — the same words the
    // rail shows, minus the spec prefix the rail already renders as a heading, so a reader
    // recognises the row instead of decoding a class name.
    private static RunStepFacts ToFacts(RunStepView step) =>
        new(step.StepIndex, step.PhaseId,
            (long)Math.Round((step.DurationSeconds ?? 0) * 1000),
            Unprefixed(step.DisplayName ?? step.StepName, step.PhaseId));

    private static string Unprefixed(string name, string? specId) =>
        specId is not null && name.StartsWith($"{specId}: ", StringComparison.Ordinal)
            ? name[(specId.Length + 2)..]
            : name;
}
