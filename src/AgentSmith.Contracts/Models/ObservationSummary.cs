namespace AgentSmith.Contracts.Models;

/// <summary>
/// Aggregated severity + review counts from a list of SkillObservations.
/// Replaces FindingSummary (p0123).
/// </summary>
public sealed record ObservationSummary(
    int Total, int High, int Medium, int Low, int Info,
    int Confirmed, int NotReviewed)
{
    public static ObservationSummary From(IReadOnlyList<SkillObservation> observations) => new(
        observations.Count,
        observations.Count(o => o.Severity == ObservationSeverity.High),
        observations.Count(o => o.Severity == ObservationSeverity.Medium),
        observations.Count(o => o.Severity == ObservationSeverity.Low),
        observations.Count(o => o.Severity == ObservationSeverity.Info),
        observations.Count(o => o.ReviewStatus == "confirmed"),
        observations.Count(o => o.ReviewStatus == "not_reviewed"));
}
