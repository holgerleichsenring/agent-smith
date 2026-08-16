using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: turns findings nobody vouched for into candidates a refuter can be shown,
/// dropping the ones whose citation resolves against no file the scan can read.
/// </summary>
public interface ICandidateFindingFactory
{
    Task<CandidateSet> BuildAsync(
        IReadOnlyList<SkillObservation> unvouched,
        ISandboxFileReader reader,
        CancellationToken cancellationToken);
}
