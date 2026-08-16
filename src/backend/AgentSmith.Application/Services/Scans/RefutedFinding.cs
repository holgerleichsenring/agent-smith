using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: what a refuted finding becomes. It is DOWNGRADED, never deleted.
/// <para>
/// Deleting it would make the scan quieter by hiding a disagreement: a scanner said one
/// thing, a reader said another, and the reviewer is the one entitled to decide. So the
/// finding survives below the Critical/High bar that drives escalation and auto-fix, and
/// carries the refutation with it — the reviewer reads why, in one line, instead of
/// working it out again.
/// </para>
/// </summary>
public static class RefutedFinding
{
    public const string ReviewStatus = "refuted";

    public static SkillObservation Downgrade(SkillObservation finding, string? why) =>
        finding with
        {
            Severity = ObservationSeverity.Medium,
            Blocking = false,
            ReviewStatus = ReviewStatus,
            Rationale = Compose(finding.Rationale, why),
        };

    private static string Compose(string? rationale, string? why) =>
        $"Refuted against the cited source: {why ?? "no reason given"}."
        + (string.IsNullOrWhiteSpace(rationale) ? string.Empty : $" Originally: {rationale}");
}
