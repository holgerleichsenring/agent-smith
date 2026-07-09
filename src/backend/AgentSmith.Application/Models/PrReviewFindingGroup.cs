using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Review observations grouped at one inline anchor (file + inclusive
/// new-side line span). TopSeverity is the most severe grouped observation —
/// the group's sort key and the severity shown on the rendered comment.
/// </summary>
public sealed record PrReviewFindingGroup(
    string File,
    int StartLine,
    int EndLine,
    IReadOnlyList<SkillObservation> Observations)
{
    public ObservationSeverity TopSeverity => Observations.Min(o => o.Severity);
}
