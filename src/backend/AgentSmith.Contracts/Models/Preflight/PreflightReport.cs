namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0324: the aggregated preflight run. <see cref="ExitCode"/> encodes the CLI
/// contract — 0 all green, 1 on any failure (failure count capped at 1); skips
/// never fail the run.
/// </summary>
public sealed record PreflightReport(IReadOnlyList<PreflightCheckOutcome> Outcomes)
{
    public int PassedCount => Count(PreflightStatus.Pass);

    public int FailedCount => Count(PreflightStatus.Fail);

    public int SkippedCount => Count(PreflightStatus.Skip);

    public bool HasFailures => FailedCount > 0;

    public int ExitCode => HasFailures ? 1 : 0;

    private int Count(PreflightStatus status) =>
        Outcomes.Count(o => o.Result.Status == status);
}
