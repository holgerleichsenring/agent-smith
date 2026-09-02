using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: sorts findings into the three fates a scan can give them, resolving each
/// citation against the evidence the run really holds.
/// </summary>
public interface ICandidateFindingFactory
{
    Task<CandidateSet> BuildAsync(
        IReadOnlyList<SkillObservation> findings,
        ScanEvidence evidence,
        CancellationToken cancellationToken);
}
